using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.CardPiles;

namespace GuZhenRenRubild.Cards;

/// <summary>
/// 管理战斗内蛊牌的三个专用牌堆，并实现完整循环：储备区 → 激活区 → 恢复区 → 储备区。
/// 激活区以额外手牌形式展示，可直接打出；储备区保存等待补位的蛊牌；恢复区保存使用次数耗尽、尚未恢复的蛊牌。
/// </summary>
public static class GuCardPileSystem
{
    // 激活区最多同时展示的蛊牌数量，空位会从储备区按顺序自动补充。
    public const int ActiveCapacity = 5;

    // 三个本地标识会与模组编号组合成全局唯一牌堆编号。
    private const string ActiveLocalId = "gu_active";
    private const string StorageLocalId = "gu_storage";
    private const string RecoveryLocalId = "gu_recovery";
    private const string PileIconPath =
        $"res://{Entry.ModId}/materials/GuPile.svg";

    // 注册过程使用互斥锁和初始化标记，避免多入口重复注册同名牌堆。
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    // 注册完成后缓存 RitsuLib 返回的牌堆类型，后续所有移动操作都使用这些类型。
    public static PileType ActivePileType { get; private set; }
    public static PileType StoragePileType { get; private set; }
    public static PileType RecoveryPileType { get; private set; }

    // 向 RitsuLib 注册三个仅在战斗期间存在的蛊牌牌堆及其界面表现。
    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            ModCardPileRegistry registry = ModCardPileRegistry.For(Entry.ModId);

            // 激活区使用额外手牌样式，允许直接打出，并显示原生“可打出”高亮。
            ActivePileType = registry.RegisterOwned(
                ActiveLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.ExtraHand,
                    CardShouldBeVisible = true,
                    ExtraHand = new ModCardPileExtraHandSpec
                    {
                        AllowCardPlay = true,
                        ShowPlayableGlow = true,
                    },
                }
            ).PileType;

            // 储备区显示在左下角主位置，用于保存尚未进入激活区的蛊牌。
            StoragePileType = registry.RegisterOwned(
                StorageLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomLeft,
                    IconPath = PileIconPath,
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftPrimary,
                        Vector2.Zero
                    ),
                    CardShouldBeVisible = true,
                }
            ).PileType;

            // 恢复区位于储备区旁边，用于保存等待恢复倒计时结束的蛊牌。
            RecoveryPileType = registry.RegisterOwned(
                RecoveryLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomLeft,
                    IconPath = PileIconPath,
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftSecondary,
                        new Vector2(-190f, 0f)
                    ),
                    CardShouldBeVisible = true,
                }
            ).PileType;

            _initialized = true;
        }
    }

    public static void Uninitialize()
    {
        // RitsuLib 的牌堆注册在整个进程内长期有效，当前没有安全的反注册接口。
        // 因此卸载阶段不清空初始化标记，避免随后再次初始化时重复注册相同编号。
    }

    // 战斗开始时重建蛊牌布局：先收集所有可能位置中的蛊牌，重置其战斗状态，统一放回储备区，再补满激活区。
    internal static void InitializeCombat(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();

        CardPile active = ActivePileType.GetPile(owner);
        CardPile storage = StoragePileType.GetPile(owner);
        CardPile recovery = RecoveryPileType.GetPile(owner);
        CardPile[] allPiles =
        [
            PileType.Draw.GetPile(owner),
            PileType.Discard.GetPile(owner),
            PileType.Hand.GetPile(owner),
            active,
            storage,
            recovery,
        ];

        // 同一张卡可能在不同来源列表中被枚举到，Distinct 可防止重复移动或重复重置。
        CardModel[] guCards = allPiles
            .SelectMany(static pile => pile.Cards)
            .Where(static card => card is IGuCard)
            .Distinct()
            .ToArray();

        // 批量移动时先静默修改牌堆内容，最后只对真正变化过的牌堆各发送一次变更通知。
        HashSet<CardPile> changed = [];
        foreach (CardModel card in guCards)
        {
            GuCardRuntime.ResetForCombat(card);
            MoveWithoutNotification(card, storage, changed);
        }

        // 按储备区当前顺序取前若干张蛊牌进入激活区，最多不超过容量上限。
        int activeCount = 0;
        foreach (CardModel card in storage.Cards.ToArray())
        {
            if (activeCount >= ActiveCapacity)
            {
                break;
            }

            MoveWithoutNotification(card, active, changed);
            activeCount++;
        }

        foreach (CardPile pile in changed)
        {
            pile.InvokeContentsChanged();
        }
    }

    // 把额外手牌中的蛊牌移入原生手牌区，供原生打牌校验和联机流程继续处理。
    internal static void MoveToNativeHand(CardModel card)
    {
        MoveCardWithoutAnimation(card, PileType.Hand.GetPile(card.Owner));
    }

    // 通过原生牌堆命令异步移动卡牌；如果目标牌堆与当前位置相同则直接返回，保证操作幂等。
    internal static async Task MoveCardToPileAsync(
        CardModel card,
        PileType targetPile,
        bool skipVisuals
    )
    {
        if (card.Pile?.Type == targetPile)
        {
            return;
        }

        await CardPileCmd.Add(
            card,
            targetPile,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: skipVisuals
        );
    }

    // 在回合刷新时检查恢复区，将达到恢复回合的蛊牌重置使用次数并送回储备区，随后补充激活区。
    public static async Task RestoreRecoveredCardsAsync(
        Player owner,
        int turnNumber
    )
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();

        CardModel[] recovered = RecoveryPileType
            .GetPile(owner)
            .Cards
            .Where(card => GuCardRuntime.IsRecoveryReady(card, turnNumber))
            .ToArray();

        foreach (CardModel card in recovered)
        {
            GuCardRuntime.CompleteRecovery(card);
            await MoveCardToPileAsync(card, StoragePileType, skipVisuals: false);
        }

        await RefillActiveAsync(owner, skipVisuals: false);
    }

    // 持续从储备区首部向激活区补牌，直到激活区满、储备区为空或底层移动命令失败。
    public static async Task RefillActiveAsync(Player owner, bool skipVisuals)
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();

        CardPile active = ActivePileType.GetPile(owner);
        CardPile storage = StoragePileType.GetPile(owner);

        while (active.Cards.Count < ActiveCapacity && storage.Cards.Count > 0)
        {
            CardModel next = storage.Cards.First();
            CardPileAddResult result = await CardPileCmd.Add(
                next,
                ActivePileType,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: skipVisuals
            );

            // 失败后立即停止循环，避免同一张无法移动的牌导致无限重试。
            if (!result.success)
            {
                Entry.Logger.Warn($"Could not refill Gu card {next.Id}.");
                break;
            }
        }
    }

    // 用内部牌堆接口执行无动画移动，并在完成后统一触发内容变更事件。
    private static void MoveCardWithoutAnimation(CardModel card, CardPile target)
    {
        // 批量移动时先静默修改牌堆内容，最后只对真正变化过的牌堆各发送一次变更通知。
        HashSet<CardPile> changed = [];
        MoveWithoutNotification(card, target, changed);
        foreach (CardPile pile in changed)
        {
            pile.InvokeContentsChanged();
        }
    }

    // 最底层的静默移动：不播放动画、不立即广播事件，只把变化过的源牌堆和目标牌堆记录到集合中。
    private static void MoveWithoutNotification(
        CardModel card,
        CardPile target,
        HashSet<CardPile> changed
    )
    {
        CardPile? source = card.Pile;
        if (ReferenceEquals(source, target))
        {
            return;
        }

        source?.RemoveInternal(card, silent: true);
        target.AddInternal(card, silent: true);
        if (source != null)
        {
            changed.Add(source);
        }
        changed.Add(target);
    }

    // 所有战斗入口都允许惰性初始化，避免调用顺序变化时出现未注册牌堆。
    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
