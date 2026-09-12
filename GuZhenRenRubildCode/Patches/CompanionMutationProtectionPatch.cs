using System.Reflection;
using GuZhenRenRubild.Cards.Companions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>玩家侧伴生牌删除/Transform 保护。</summary>
internal static class CompanionMutationProtectionPatch
{
    private const string HarmonyId = Entry.ModId + ".CompanionMutationProtection";
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo isRemovable = AccessTools.PropertyGetter(
            typeof(CardModel), nameof(CardModel.IsRemovable)
        ) ?? throw new MissingMethodException(typeof(CardModel).FullName, nameof(CardModel.IsRemovable));
        MethodInfo removeFromDeck = AccessTools.Method(
            typeof(CardPileCmd),
            nameof(CardPileCmd.RemoveFromDeck),
            [typeof(IReadOnlyList<CardModel>), typeof(bool)]
        ) ?? throw new MissingMethodException(typeof(CardPileCmd).FullName, nameof(CardPileCmd.RemoveFromDeck));

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            isRemovable,
            postfix: new HarmonyMethod(typeof(CompanionMutationProtectionPatch), nameof(IsRemovablePostfix))
        );
        harmony.Patch(
            removeFromDeck,
            prefix: new HarmonyMethod(typeof(CompanionMutationProtectionPatch), nameof(RemoveFromDeckPrefix))
        );
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
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
