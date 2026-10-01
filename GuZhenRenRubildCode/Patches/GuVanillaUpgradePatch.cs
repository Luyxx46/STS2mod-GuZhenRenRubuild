using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-01 的执行层：原版/事件的升级效果作用于蛊牌时，把「升级」映射为「升 1 转」。
///
/// <para>
/// 资格由 <see cref="AbstractGuCard.MaxUpgradeLevel"/> 决定（非仙蛊且未封顶 ⇒ 可升）；
/// 本补丁拦截 <see cref="CardModel.UpgradeInternal"/>，跳过原生的
/// CurrentUpgradeLevel++/OnUpgrade，改走 <c>TryIncreaseGuRank</c>（含仙蛊唯一性仲裁，
/// 5→6 转跨界时由仲裁兜底）。拦截后 CurrentUpgradeLevel 恒为 0，
/// 因此 IsUpgraded 恒 false，Reflections/WelcomeToWongos 等「降级已升级牌」效果永不选中蛊牌。
/// </para>
///
/// <para>
/// 与篝火升炼预览（<see cref="GuRankUpPreviewPatch"/>，Priority.First）共用同一目标方法：
/// 预览作用域内由它拦截，作用域外由本补丁接手；这里的 IsActive 检查是双保险，
/// 即使未来优先级被调整也不会出现双重升转。
/// </para>
///
/// <para>
/// 已知观感限制：事件升级界面的「升级后」预览走 UpgradeDisplay/OnUpgrade，
/// 蛊牌未 override OnUpgrade，预览文案与当前一致（{Rank} 不变），实际执行会 +1 转。
/// </para>
/// </summary>
internal static class GuVanillaUpgradePatch
{
    private const string HarmonyId = Entry.ModId + ".GuVanillaUpgrade";

    // 升转成功后手动触发原版的 Upgraded 事件，让卡面数值立即刷新
    // （原生 UpgradeInternal 会触发它，拦截后必须自己补上）。
    private static readonly FieldInfo? UpgradedEventField =
        AccessTools.Field(typeof(CardModel), nameof(CardModel.Upgraded));

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo upgradeInternal = RequiredMember.DeclaredMethod(
                typeof(CardModel),
                nameof(CardModel.UpgradeInternal),
                Type.EmptyTypes
            );

            harmony.Patch(
                upgradeInternal,
                prefix: new HarmonyMethod(
                    typeof(GuVanillaUpgradePatch),
                    nameof(UpgradeInternalPrefix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static bool UpgradeInternalPrefix(CardModel __instance)
    {
        // 非蛊牌走原版；升炼预览作用域内让位给 GuRankUpPreviewPatch。
        if (__instance is not AbstractGuCard gu ||
            GuRankUpPreviewSupport.IsActive)
        {
            return true;
        }

        // 升 1 转：封顶（MaxUpgradeLevel 已拦）或仲裁拒绝时静默不升，绝不动原生升级状态。
        bool upgraded = gu.TryIncreaseGuRank();

        if (upgraded)
        {
            (UpgradedEventField?.GetValue(__instance) as Action)?.Invoke();
        }
        else
        {
            Entry.Logger.Info(
                $"原版升级作用于蛊牌 {__instance.Id} 时未升转" +
                "（封顶或仙蛊唯一性仲裁拒绝）。"
            );
        }

        return false;
    }
}
