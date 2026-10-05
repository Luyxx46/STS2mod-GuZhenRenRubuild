using System.Reflection;

using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-14（方案 B）：玩家死亡时把 4 个蛊牌堆并入原版的死亡清理。
///
/// <para>
/// 原版 <c>CombatManager.HandlePlayerDeath</c> 只把手牌/抽牌堆/弃牌堆/消耗堆/出牌堆
/// 五个原生堆的牌 <c>CardPileCmd.RemoveFromCombat</c>（移出战斗堆并回收卡节点与动画）；
/// 激活区/储备/恢复/封存四个蛊牌堆不在 <c>PlayerCombatState.AllPiles</c> 里，
/// 死亡瞬间不会被清理，可能残留卡节点与联机网络编号。本补丁在原方法完成后
/// 对四个蛊堆执行同样的 <c>RemoveFromCombat</c>，与原生五堆的死亡表现对齐。
/// </para>
///
/// <para>
/// 封存区里的杀招材料被移除时会触发 <see cref="ShaZhaoBindingPatch"/> 的解绑逻辑
/// （与战后 <c>AfterCombatEnd</c> 的兜底语义一致）；对非古月方源玩家，四个蛊堆
/// 恒为空，<c>RemoveFromCombat</c> 对空集合直接返回，零影响。
/// </para>
/// </summary>
internal static class GuDeathCleanupPatch
{
    private const string HarmonyId = Entry.ModId + ".GuDeathCleanup";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo handlePlayerDeath = RequiredMember.Method(
                typeof(CombatManager),
                nameof(CombatManager.HandlePlayerDeath),
                [typeof(Player)]
            );

            harmony.Patch(
                handlePlayerDeath,
                postfix: new HarmonyMethod(
                    typeof(GuDeathCleanupPatch),
                    nameof(HandlePlayerDeathPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    // 异步包装（与 GuTransformRankPatch 同模式）：等原方法的清理完成后再清理蛊堆。
    private static void HandlePlayerDeathPostfix(
        Player player,
        ref Task __result
    )
    {
        __result = CleanupGuPilesAfterDeathAsync(__result, player);
    }

    private static async Task CleanupGuPilesAfterDeathAsync(
        Task deathTask,
        Player player
    )
    {
        await deathTask;

        // 与原方法同条件：死亡处理期间战斗仍在进行；若此刻战斗已结束，
        // 说明清理已由战后回收流程接管，不再重复处理。
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        CardPile[] guPiles =
        [
            GuCardPileSystem.ActivePileType.GetPile(player),
            GuCardPileSystem.StoragePileType.GetPile(player),
            GuCardPileSystem.RecoveryPileType.GetPile(player),
            GuCardPileSystem.SealedPileType.GetPile(player),
        ];

        CardModel[] cards = guPiles
            .SelectMany(static pile => pile.Cards)
            .ToArray();

        if (cards.Length == 0)
        {
            return;
        }

        await CardPileCmd.RemoveFromCombat(cards);

        Entry.Logger.Info(
            $"玩家死亡清理：已把 {cards.Length} 张蛊牌移出战斗牌堆（D-14）。"
        );
    }
}
