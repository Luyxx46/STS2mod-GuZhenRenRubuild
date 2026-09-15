using GuZhenRenRubild.Cards.Core.ImmortalEssence;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Cards.ShaZhao;
using GuZhenRenRubild.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace GuZhenRenRubild.Aperture;

/// <summary>
/// 空窍/仙窍运行时。
///
/// 所有需要跨保存、重连和多人快照恢复的状态均写入 RitsuLib 的
/// Run Saved Data（按玩家保存），因此这里只负责"何时推进、何时发放奖励"，
/// 不再自行维护存档序列化。
/// </summary>
public static class ApertureSystem
{
    private const string SavedDataKey = "aperture";

    private static readonly object SyncRoot = new();
    private static PlayerRunSavedData<ApertureRunData>? _savedData;
    private static bool _initialized;

    public static bool IsInitialized => _initialized && _savedData != null;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            if (_savedData == null)
            {
                using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
                {
                    _savedData = RitsuLibFramework
                        .GetRunSavedDataStore(Entry.ModId)
                        .RegisterPerPlayer<ApertureRunData>(
                            SavedDataKey,
                            static () => new ApertureRunData(),
                            options: new RunSavedDataOptions
                            {
                                SchemaVersion = 1,
                            }
                        );
                }
            }

            _initialized = true;
            Entry.Logger.Info(
                "空窍运行时初始化完成（转数与修为已接入运行快照）。"
            );
        }
    }

    public static void Uninitialize()
    {
        lock (SyncRoot)
        {
            _initialized = false;
        }
    }

    /// <summary>
    /// 读取指定玩家的空窍数据；数据异常时先就地规范化再返回。
    /// </summary>
    public static ApertureRunData GetState(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        ApertureRunData data = _savedData!.Get(player);

        return data.NeedsNormalization()
            ? _savedData.Modify(player, static value => value.Normalize())
            : data;
    }

    /// <summary>
    /// 战斗胜利结算：按房间难度发放修为，提交转数变化，
    /// 再以幂等步骤补发最大生命奖励。
    ///
    /// 胜利回调可能在网络恢复或房间重建中重放，因此用运行层数做去重标记，
    /// 并把"层数标记 + 修为变更"放在同一次 Modify 中提交。
    /// </summary>
    internal static async Task HandleCombatVictoryAsync(
        Player player,
        CombatRoom room
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(room);

        if (!IsInitialized || FindApertureRelic(player) == null)
        {
            return;
        }

        int currentFloor = player.RunState.TotalFloor;
        ApertureTransition transition = default;

        _savedData!.Modify(
            player,
            data =>
            {
                data.Normalize();

                if (data.VictoryXpAppliedFloor == currentFloor)
                {
                    return;
                }

                data.VictoryXpAppliedFloor = currentFloor;
                transition = ApertureProgression.GainVictoryXp(
                    data,
                    GetVictoryXp(room)
                );
            }
        );

        await ApplyMaxHpAwardsAsync(player);

        if (transition.RankChanged)
        {
            Entry.Logger.Info(
                $"空窍突破：{transition.PreviousRank} 转 -> " +
                $"{transition.CurrentRank} 转。"
            );
        }

        RefreshRelicVisualState(player);
    }

    /// <summary>
    /// 把当前转数同步到空窍遗物的图标与数值显示。
    /// </summary>
    internal static void RefreshRelicVisualState(Player player)
    {
        GuZhenRenRubildRelic? relic = FindApertureRelic(player);

        if (relic == null || _savedData == null)
        {
            return;
        }

        try
        {
            relic.RefreshApertureVisualState(GetState(player));
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn($"刷新空窍遗物显示失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 战斗开始事务：把"本场战斗"的进度重置一次。
    ///
    /// BeforeCombatStart 可能在同一场战斗的重连恢复或房间重建中再次触发，
    /// 因此用运行层数做去重标记，只有层数变化才视为一场新战斗。
    /// </summary>
    internal static void HandleCombatStarting(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!IsInitialized || FindApertureRelic(player) == null)
        {
            return;
        }

        int currentFloor = player.RunState.TotalFloor;

        _savedData!.Modify(
            player,
            data =>
            {
                data.Normalize();

                if (data.ActiveCombatFloor == currentFloor)
                {
                    return;
                }

                data.ActiveCombatFloor = currentFloor;
                data.ShaZhaoDerivationGrantFloor = -1;
                data.ShaZhaoDerivationsThisCombat = 0;
                data.XianYuanGrantFloor = -1;
            }
        );
    }

    /// <summary>
    /// 空窍三转起，每场战斗开始时把"杀招推演"悬停到蛊手牌固定位（仙元牌之后），
    /// 不占起手抽牌，也不占蛊牌补位槽。同一运行层数只发放一次（重连安全）。
    ///
    /// 杀招池为空时不发放：此时没有任何可推演的目标，
    /// 发牌只会让玩家拿到一张必然失败的系统牌。
    /// </summary>
    internal static async Task HandleShaZhaoDerivationGrantAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!IsInitialized || FindApertureRelic(player) == null)
        {
            return;
        }

        if (!ShaZhaoRecipeRegistry.HasAnyRecipe)
        {
            return;
        }

        ApertureRunData data = GetState(player);

        if (data.Rank < ApertureProgression.ShaZhaoDerivationUnlockRank)
        {
            return;
        }

        if (player.Creature.CombatState is not { } combatState)
        {
            return;
        }

        int currentFloor = player.RunState.TotalFloor;

        if (data.ShaZhaoDerivationGrantFloor == currentFloor)
        {
            return;
        }

        CardModel derivation = combatState.CreateCard(
            ModelDb.Card<ShaZhaoTuiYan>(),
            player
        );

        await CardPileCmd.AddGeneratedCardToCombat(
            derivation,
            GuCardPileSystem.ActivePileType,
            player
        );

        // 固定位排序：杀招推演牌必须排在仙元牌之后（发放顺序在它之前执行）。
        GuCardPileSystem.NormalizePinnedCardOrder(player);

        _savedData!.Modify(
            player,
            d =>
            {
                d.Normalize();

                if (d.ShaZhaoDerivationGrantFloor != currentFloor)
                {
                    d.ShaZhaoDerivationGrantFloor = currentFloor;
                }
            }
        );
    }

    /// <summary>
    /// 空窍六转（仙窍）起，每场战斗开始时把对应档位的"仙元"货币牌加入
    /// 蛊手牌堆：不占起手抽牌，也不占蛊牌补位槽（见
    /// <c>GuCardPileSystem.CountActiveGu</c>）。同一运行层数只发放一次（重连安全）。
    /// </summary>
    internal static async Task HandleXianYuanGrantAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!IsInitialized || FindApertureRelic(player) == null)
        {
            return;
        }

        ApertureRunData data = GetState(player);

        if (data.Rank < ApertureProgression.ImmortalRank)
        {
            return;
        }

        if (player.Creature.CombatState is not { } combatState)
        {
            return;
        }

        int currentFloor = player.RunState.TotalFloor;

        if (data.XianYuanGrantFloor == currentFloor)
        {
            return;
        }

        if (ImmortalEssenceSystem.GetEssenceCardForRank(data.Rank) is not
            { } essenceModel)
        {
            return;
        }

        CardModel essence = combatState.CreateCard(essenceModel, player);

        await CardPileCmd.AddGeneratedCardToCombat(
            essence,
            GuCardPileSystem.ActivePileType,
            player
        );

        // 固定位排序：仙元牌必须保持在蛊手牌最左端。
        GuCardPileSystem.NormalizePinnedCardOrder(player);

        _savedData!.Modify(
            player,
            d =>
            {
                d.Normalize();

                if (d.XianYuanGrantFloor != currentFloor)
                {
                    d.XianYuanGrantFloor = currentFloor;
                }
            }
        );
    }

    /// <summary>
    /// 推演成功后登记次数（观测用）。八转起每场 2 次的上限改由
    /// "杀招推演"牌自带的催动次数表达，不再单独补发第二张牌。
    /// </summary>
    internal static void RegisterShaZhaoDerivation(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!IsInitialized || FindApertureRelic(player) == null)
        {
            return;
        }

        ApertureRunData data = GetState(player);

        if (data.Rank < ApertureProgression.ShaZhaoDerivationUnlockRank)
        {
            return;
        }

        int completed = data.ShaZhaoDerivationsThisCombat + 1;

        _savedData!.Modify(
            player,
            d =>
            {
                d.Normalize();
                d.ShaZhaoDerivationsThisCombat = Math.Max(0, completed);
            }
        );
    }

    /// <summary>
    /// 按房间难度发放修为：首领 5、精英 3、普通 1。
    /// </summary>
    private static int GetVictoryXp(CombatRoom room)
    {
        return room.RoomType switch
        {
            RoomType.Boss => 5,
            RoomType.Elite => 3,
            _ => 1,
        };
    }

    /// <summary>
    /// 逐个补发六至九转的最大生命奖励。
    ///
    /// 发放过程分两步提交：先记录"正在结算的转数与结算前的最大生命"，
    /// 执行加最大生命命令后再推进已结算进度。这样即使进程在命令成功、
    /// 进度未提交之间中断，重连后也能按目标上限补齐差额而不会重复获得。
    /// </summary>
    private static async Task ApplyMaxHpAwardsAsync(Player player)
    {
        while (true)
        {
            ApertureRunData state = GetState(player);

            if (state.MaxHpAwardInProgressRank > 0)
            {
                int awardRank = state.MaxHpAwardInProgressRank;
                int expectedMaxHp = state.MaxHpBeforePendingAward +
                    ApertureProgression.GetMaxHpAward(awardRank);
                decimal missingMaxHp = Math.Max(
                    0m,
                    expectedMaxHp - player.Creature.MaxHp
                );

                if (missingMaxHp > 0m)
                {
                    await CreatureCmd.GainMaxHp(
                        player.Creature,
                        missingMaxHp
                    );
                }

                _savedData!.Modify(
                    player,
                    data =>
                    {
                        data.Normalize();

                        if (data.MaxHpAwardInProgressRank != awardRank)
                        {
                            return;
                        }

                        data.MaxHpAppliedThroughRank = Math.Max(
                            data.MaxHpAppliedThroughRank,
                            awardRank
                        );
                        data.MaxHpAwardInProgressRank = 0;
                        data.MaxHpBeforePendingAward = 0;
                    }
                );

                continue;
            }

            // 凡人阶段没有奖励，进度天然等于当前转数，因此从六转开始检查。
            int nextRank = Math.Max(
                ApertureProgression.ImmortalRank,
                state.MaxHpAppliedThroughRank + 1
            );

            if (nextRank > state.Rank)
            {
                return;
            }

            int award = ApertureProgression.GetMaxHpAward(nextRank);

            if (award <= 0)
            {
                // 该转数没有奖励（理论上不会出现），直接推进进度避免死循环。
                _savedData!.Modify(
                    player,
                    data =>
                    {
                        data.Normalize();
                        data.MaxHpAppliedThroughRank = Math.Max(
                            data.MaxHpAppliedThroughRank,
                            nextRank
                        );
                    }
                );

                continue;
            }

            int maxHpBeforeAward = (int)player.Creature.MaxHp;
            bool reserved = false;

            _savedData!.Modify(
                player,
                data =>
                {
                    data.Normalize();

                    if (data.MaxHpAwardInProgressRank != 0 ||
                        data.MaxHpAppliedThroughRank >= nextRank)
                    {
                        return;
                    }

                    data.MaxHpAwardInProgressRank = nextRank;
                    data.MaxHpBeforePendingAward = maxHpBeforeAward;
                    reserved = true;
                }
            );

            if (!reserved)
            {
                // 已有其他结算路径接手该转数，交由它完成，避免重复发放。
                return;
            }
        }
    }

    private static GuZhenRenRubildRelic? FindApertureRelic(Player player)
    {
        return player.Relics
            .OfType<GuZhenRenRubildRelic>()
            .FirstOrDefault();
    }

    private static void EnsureAvailable()
    {
        if (!_initialized || _savedData == null)
        {
            throw new InvalidOperationException("空窍运行时尚未初始化。");
        }
    }
}
