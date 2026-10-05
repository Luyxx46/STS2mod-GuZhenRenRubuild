using System.Reflection;
using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using GuZhenRenRubild.Common.Scoping;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 只在 CardFactory.CreateForReward 调用链中过滤奖励候选，避免污染 Transform、药水或战斗生成。
///
/// <para>
/// 裁决 D-11（方案 A，回退放行）：对古月方源，<see cref="GuCardRewardRules.CanAppear"/>
/// 只放行蛊牌，因此无色池 / 其他角色卡池之类的来源会被整体滤空。原版调用方有不少
/// 用 <c>.First()</c> 直接取结果（EndlessConveyor 炸鳗鱼、AllStar 修正器、Kaleidoscope
/// 万花筒都是必崩点），返回空集合会让它们抛 <see cref="InvalidOperationException"/>。
/// 滤空时本补丁不再返回空集，而是退出过滤作用域并放行原方法，让原版按自己的
/// 候选执行（即回到未装模组的行为）；放行期间 <c>TryModifyCardRewardOptions</c>
/// 的剔除也一并跳过，防止刚放行的候选又被删光。
/// </para>
/// </summary>
internal static class GuCardRewardPatch
{
    private const string HarmonyId = Entry.ModId + ".GuCardReward";

    // 标记"当前调用链正处于奖励候选查询中"，使深层调用也能识别这一次查询的来源。
    private static readonly AmbientFlag RewardCandidateQuery = new();

    // 裁决 D-11：候选滤空后的"回退放行中"标记。激活期间不剔除任何候选，
    // 作用域精确到被放行的这一次 CreateForReward 调用（postfix/finalizer 收尾）。
    private static readonly AmbientFlag RewardFallbackActive = new();

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo getPossibleCards = RequiredMember.Method(
                typeof(CardCreationOptions),
                nameof(CardCreationOptions.GetPossibleCards),
                [typeof(Player)]
            );
            MethodInfo createForReward = RequiredMember.Method(
                typeof(CardFactory),
                nameof(CardFactory.CreateForReward),
                [typeof(Player), typeof(int), typeof(CardCreationOptions)]
            );
            MethodInfo modifyRewardOptions = RequiredMember.Method(
                typeof(Hook),
                nameof(Hook.TryModifyCardRewardOptions),
                [
                    typeof(IRunState),
                    typeof(Player),
                    typeof(List<CardCreationResult>),
                    typeof(CardCreationOptions),
                    typeof(List<AbstractModel>).MakeByRefType(),
                ]
            );

            harmony.Patch(
                getPossibleCards,
                postfix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(GetPossibleCardsPostfix))
            );
            harmony.Patch(
                createForReward,
                prefix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(CreateForRewardPrefix)),
                postfix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(CreateForRewardPostfix)),
                finalizer: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(CreateForRewardFinalizer))
            );
            harmony.Patch(
                modifyRewardOptions,
                postfix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(ModifyRewardOptionsPostfix))
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch(static () =>
        {
            RewardCandidateQuery.Reset();
            RewardFallbackActive.Reset();
        });
    }

    private static void GetPossibleCardsPostfix(
        Player player,
        ref IEnumerable<CardModel> __result
    )
    {
        if (!RewardCandidateQuery.IsActive)
        {
            return;
        }

        __result = __result
            .GroupBy(static card => card.Id)
            .Select(static group => group.First())
            .Where(card => GuCardRewardRules.CanAppear(player, card))
            .ToArray();
    }

    private static bool CreateForRewardPrefix(
        Player player,
        ref int cardCount,
        CardCreationOptions options,
        ref IEnumerable<CardCreationResult> __result,
        out IDisposable __state
    )
    {
        RewardQueryScope scope = new(RewardCandidateQuery.Enter());
        __state = scope;

        CardModel[] possibleCards = options
            .GetPossibleCards(player)
            .Where(card => GuCardRewardRules.CanAppear(player, card))
            .Where(card =>
                options.RarityOdds != CardRarityOddsType.Uniform ||
                (card.Rarity != CardRarity.Basic && card.Rarity != CardRarity.Ancient)
            )
            .GroupBy(static card => card.Id)
            .Select(static group => group.First())
            .ToArray();

        if (possibleCards.Length == 0)
        {
            // 裁决 D-11（方案 A）：滤空说明这个来源"本来就不该给蛊"（无色池、
            // 其他角色卡池等）。原版调用方多用 .First() 取结果，返回空集合必崩；
            // 因此回退为"不过滤"——提前退出查询作用域并放行原方法，让原版按
            // 自己的候选执行（等同未装模组的行为）。回退标记同时让
            // ModifyRewardOptionsPostfix 跳过剔除，防止放行的候选又被删光。
            scope.EnterFallback();
            Entry.Logger.Info(
                $"Player {player.NetId} has no eligible Gu card reward candidates; "
                + "falling back to unfiltered vanilla candidates."
            );
            return true;
        }

        cardCount = Math.Min(cardCount, possibleCards.Length);
        return true;
    }

    private static void CreateForRewardPostfix(IDisposable __state)
    {
        __state.Dispose();
    }

    private static Exception? CreateForRewardFinalizer(
        Exception? __exception,
        IDisposable __state
    )
    {
        __state.Dispose();
        return __exception;
    }

    [HarmonyPriority(Priority.Last)]
    private static void ModifyRewardOptionsPostfix(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        ref bool __result
    )
    {
        // 裁决 D-11：空回退中的这次调用不过滤——放行的原版候选（无色牌/他角色牌）
        // 对古月方源本来就不满足 CanAppear，再剔除就又回到空集合、重新引爆调用方。
        if (RewardFallbackActive.IsActive)
        {
            return;
        }

        int removed = cardRewardOptions.RemoveAll(result =>
            !GuCardRewardRules.CanAppear(player, result.Card)
        );
        if (removed > 0)
        {
            __result = true;
        }
    }

    /// <summary>
    /// 一次 CreateForReward 调用的作用域状态：正常路径只持有查询标记；
    /// 触发空回退（裁决 D-11）时提前退出查询标记并进入回退标记。
    /// 两个标记由同一个 <see cref="Dispose"/> 收尾，postfix 与 finalizer
    /// 的重复释放是安全的（<see cref="AmbientFlag"/> 的 Scope 幂等）。
    /// </summary>
    private sealed class RewardQueryScope(IDisposable queryScope) : IDisposable
    {
        private IDisposable? _fallbackScope;

        internal void EnterFallback()
        {
            // 提前结束查询作用域：原方法内部的 GetPossibleCards 不再被过滤，
            // 原版将在未过滤的原始候选上执行。
            queryScope.Dispose();
            _fallbackScope = RewardFallbackActive.Enter();
        }

        public void Dispose()
        {
            queryScope.Dispose();
            _fallbackScope?.Dispose();
        }
    }
}
