using System.Reflection;
using GuZhenRenRubild.Cards.Companions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>玩家侧伴生牌删除/Transform 保护。</summary>
internal static class CompanionMutationProtectionPatch
{
    private const string HarmonyId = Entry.ModId + ".CompanionMutationProtection";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo isRemovable = RequiredMember.PropertyGetter(
                typeof(CardModel), nameof(CardModel.IsRemovable)
            );
            MethodInfo removeFromDeck = RequiredMember.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.RemoveFromDeck),
                [typeof(IReadOnlyList<CardModel>), typeof(bool)]
            );

            harmony.Patch(
                isRemovable,
                postfix: new HarmonyMethod(typeof(CompanionMutationProtectionPatch), nameof(IsRemovablePostfix))
            );
            harmony.Patch(
                removeFromDeck,
                prefix: new HarmonyMethod(typeof(CompanionMutationProtectionPatch), nameof(RemoveFromDeckPrefix))
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static void IsRemovablePostfix(CardModel __instance, ref bool __result)
    {
        if (!CompanionMutationScope.IsInternalMutationAllowed &&
            CompanionRelationshipService.IsManagedCompanion(__instance))
        {
            __result = false;
        }
    }

    private static void RemoveFromDeckPrefix(ref IReadOnlyList<CardModel> cards)
    {
        if (CompanionMutationScope.IsInternalMutationAllowed)
        {
            return;
        }

        CardModel[] filtered = cards
            .Where(card => !CompanionRelationshipService.IsManagedCompanion(card))
            .ToArray();
        if (filtered.Length != cards.Count)
        {
            Entry.Logger.Warn(
                $"Blocked {cards.Count - filtered.Length} direct companion deck removal(s)."
            );
            cards = filtered;
        }
    }
}
