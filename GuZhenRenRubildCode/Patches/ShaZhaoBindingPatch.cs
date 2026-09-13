using System.Reflection;

using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 杀招异常移出兜底。
///
/// 正常情况下杀招消耗时会先自行解绑并返还材料，再走原生消耗流程，
/// 因此本补丁不会重复处理。只有当杀招被其他效果直接移出战斗
/// （不属于"正常用完"）时，材料才需要在这里被解绑返还，
/// 避免它们整场战斗被封存在蛊封存区里。
/// </summary>
internal static class ShaZhaoBindingPatch
{
    private const string HarmonyId = Entry.ModId + ".ShaZhaoBinding";

    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo removeFromCombat = RequiredMember.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.RemoveFromCombat),
                [typeof(CardModel), typeof(bool)]
            );

            harmony.Patch(
                removeFromCombat,
                postfix: new HarmonyMethod(
                    typeof(ShaZhaoBindingPatch),
                    nameof(RemoveFromCombatPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    /// <summary>
    /// 通过 <c>__args</c> 读取被移除的卡牌，不依赖原方法的形参名，
    /// 避免游戏改形参名后补丁静默失效。
    /// </summary>
    private static void RemoveFromCombatPostfix(
        object[] __args,
        ref Task __result
    )
    {
        if (__args.OfType<AbstractShaZhaoCard>().FirstOrDefault()
            is not { } shaZhao ||
            !shaZhao.HasBoundMaterials)
        {
            return;
        }

        __result = AwaitRemovalAndFinalizeAsync(__result, shaZhao);
    }

    private static async Task AwaitRemovalAndFinalizeAsync(
        Task removalTask,
        AbstractShaZhaoCard shaZhao
    )
    {
        await removalTask;

        if (shaZhao.Owner is not Player player || !shaZhao.HasBoundMaterials)
        {
            return;
        }

        await ShaZhaoBindingService.FinalizeAsync(
            shaZhao,
            player,
            ShaZhaoBindingService.FinalizeReason.AbnormalRemoval
        );
    }
}
