using System.Reflection;
using GuZhenRenRubild.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>仅在“升炼”选择预览范围中把原生升级预览映射为 GuRank + 1。</summary>
internal static class GuRankUpPreviewPatch
{
    private const string HarmonyId = Entry.ModId + ".GuRankUpPreview";
    private static readonly FieldInfo? UpgradedEventField =
        AccessTools.Field(typeof(CardModel), nameof(CardModel.Upgraded));
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo isUpgradable = AccessTools.PropertyGetter(
            typeof(CardModel), nameof(CardModel.IsUpgradable)
        ) ?? throw new MissingMethodException(typeof(CardModel).FullName, nameof(CardModel.IsUpgradable));
        MethodInfo upgradeInternal = AccessTools.DeclaredMethod(
            typeof(CardModel), nameof(CardModel.UpgradeInternal)
        ) ?? throw new MissingMethodException(typeof(CardModel).FullName, nameof(CardModel.UpgradeInternal));

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            isUpgradable,
            postfix: new HarmonyMethod(typeof(GuRankUpPreviewPatch), nameof(IsUpgradablePostfix))
        );
        harmony.Patch(
            upgradeInternal,
            prefix: new HarmonyMethod(typeof(GuRankUpPreviewPatch), nameof(UpgradeInternalPrefix))
        );
        GuRankUpPreviewSupport.PatchUpgradeDescription(harmony);
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            GuRankUpPreviewSupport.Reset();
            _initialized = false;
        }
    }

    internal static IDisposable Begin(
        int remainingSlots,
        IEnumerable<CardModel> excludedCards
    ) => GuRankUpPreviewSupport.Begin(remainingSlots, excludedCards);

    [HarmonyPriority(Priority.Last)]
    private static void IsUpgradablePostfix(CardModel __instance, ref bool __result)
    {
        if (GuRankUpPreviewSupport.TryGetIsUpgradable(__instance, out bool previewResult))
        {
            __result = previewResult;
        }
    }

    [HarmonyPriority(Priority.First)]
    private static bool UpgradeInternalPrefix(CardModel __instance)
    {
        if (!GuRankUpPreviewSupport.IsActive || __instance is not AbstractGuCard gu)
        {
            return true;
        }

        if (GuRankUpPreviewSupport.TryIncreaseForPreview(gu))
        {
            (UpgradedEventField?.GetValue(__instance) as Action)?.Invoke();
        }

        return false;
    }
}
