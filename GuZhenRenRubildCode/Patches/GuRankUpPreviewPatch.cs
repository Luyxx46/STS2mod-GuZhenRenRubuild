using System.Reflection;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>仅在“升炼”选择预览范围中把原生升级预览映射为 GuRank + 1。</summary>
internal static class GuRankUpPreviewPatch
{
    private const string HarmonyId = Entry.ModId + ".GuRankUpPreview";
    private static readonly FieldInfo? UpgradedEventField =
        AccessTools.Field(typeof(CardModel), nameof(CardModel.Upgraded));

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo isUpgradable = RequiredMember.PropertyGetter(
                typeof(CardModel), nameof(CardModel.IsUpgradable)
            );
            MethodInfo upgradeInternal = RequiredMember.DeclaredMethod(
                typeof(CardModel), nameof(CardModel.UpgradeInternal)
            );

            harmony.Patch(
                isUpgradable,
                postfix: new HarmonyMethod(typeof(GuRankUpPreviewPatch), nameof(IsUpgradablePostfix))
            );
            harmony.Patch(
                upgradeInternal,
                prefix: new HarmonyMethod(typeof(GuRankUpPreviewPatch), nameof(UpgradeInternalPrefix))
            );
            GuRankUpPreviewSupport.PatchUpgradeDescription(harmony);
        });
    }

    internal static void Uninitialize()
    {
        // 与手写 try/finally 等价：先解除补丁，再由 Host 调用预览状态清理，最后置回未初始化。
        Host.Unpatch(GuRankUpPreviewSupport.Reset);
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
