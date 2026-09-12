using System.Reflection;
using GuZhenRenRubild.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 将游戏原生“卡牌升级”机制映射为蛊牌品阶提升，使篝火、事件等原生升级入口都能直接提升蛊牌品阶。
/// </summary>
internal static class GuRankUpgradePatch
{
    // 反射取得原生升级事件字段，用于品阶提升成功后继续触发依赖“卡牌已升级”事件的其他系统。
    private const string HarmonyId = Entry.ModId + ".GuRankUpgrade";
    private static readonly FieldInfo? UpgradedEventField =
        AccessTools.Field(typeof(CardModel), nameof(CardModel.Upgraded));
    private static bool _initialized;

    // 分别补丁 IsUpgradable、IsUpgraded 和 UpgradeInternal，使原生界面状态与蛊牌品阶保持一致。
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo isUpgradable = AccessTools.PropertyGetter(
            typeof(CardModel),
            nameof(CardModel.IsUpgradable)
        ) ?? throw new MissingMethodException(
            typeof(CardModel).FullName,
            nameof(CardModel.IsUpgradable)
        );
        MethodInfo isUpgraded = AccessTools.PropertyGetter(
            typeof(CardModel),
            nameof(CardModel.IsUpgraded)
        ) ?? throw new MissingMethodException(
            typeof(CardModel).FullName,
            nameof(CardModel.IsUpgraded)
        );
        MethodInfo upgrade = AccessTools.DeclaredMethod(
            typeof(CardModel),
            nameof(CardModel.UpgradeInternal)
        ) ?? throw new MissingMethodException(
            typeof(CardModel).FullName,
            nameof(CardModel.UpgradeInternal)
        );

        // 两个属性使用后置补丁覆盖返回值；真正升级使用前置补丁接管执行流程。
        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            isUpgradable,
            postfix: new HarmonyMethod(
                typeof(GuRankUpgradePatch),
                nameof(IsUpgradablePostfix)
            )
        );
        harmony.Patch(
            isUpgraded,
            postfix: new HarmonyMethod(
                typeof(GuRankUpgradePatch),
                nameof(IsUpgradedPostfix)
            )
        );
        harmony.Patch(
            upgrade,
            prefix: new HarmonyMethod(
                typeof(GuRankUpgradePatch),
                nameof(UpgradePrefix)
            )
        );
        _initialized = true;
    }

    // 移除本补丁组，避免重复安装相同 Harmony 前后置方法。
    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
    }

    // 对蛊牌而言，只要当前品阶低于最大品阶，就仍可通过原生入口继续升级。
    private static void IsUpgradablePostfix(CardModel __instance, ref bool __result)
    {
        if (__instance is AbstractGuCard gu)
        {
            __result = gu.GuRank < gu.MaxGuRank;
        }
    }

    // 原生“是否升级过”状态映射为“品阶是否高于最低品阶”。
    private static void IsUpgradedPostfix(CardModel __instance, ref bool __result)
    {
        if (__instance is AbstractGuCard gu)
        {
            __result = gu.GuRank > AbstractGuCard.MinimumGuRank;
        }
    }

    // 非蛊牌继续执行原生升级；蛊牌则改为提升品阶并阻止原生 UpgradeInternal 修改普通升级状态。
    private static bool UpgradePrefix(CardModel __instance)
    {
        if (__instance is not AbstractGuCard gu)
        {
            return true;
        }

        // 只有品阶实际提升成功时才触发原生 Upgraded 事件，避免满品阶时产生虚假的升级通知。
        if (gu.TryIncreaseGuRank())
        {
            (UpgradedEventField?.GetValue(__instance) as Action)?.Invoke();
        }

        return false;
    }
}
