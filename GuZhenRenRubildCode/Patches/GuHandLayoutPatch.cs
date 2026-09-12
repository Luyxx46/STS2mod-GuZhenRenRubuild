using System.Reflection;
using GuZhenRenRubild.Cards.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 蛊手牌布局补丁：在 RitsuLib 的额外手牌容器每次处理时同步蛊手牌位置，
/// 让激活区落在普通手牌后方，并跟随鼠标是否停在手牌区域自动上下平移。
/// </summary>
internal static class GuHandLayoutPatch
{
    private const string HarmonyId = Entry.ModId + ".GuHandLayout";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        // NModExtraHand._Process 是本补丁唯一的布局驱动点：找不到它就等于补丁
        // 无效。这里只记录警告而不抛异常，避免因游戏/RitsuLib 版本差异让整个模组
        // 初始化失败（Entry 会把已启动组件全部回滚）。
        MethodInfo? extraHandProcess = AccessTools.DeclaredMethod(
            typeof(NModExtraHand),
            nameof(NModExtraHand._Process),
            [typeof(double)]
        );
        if (extraHandProcess == null)
        {
            Entry.Logger.Warn(
                "[蛊手牌布局] 未找到 NModExtraHand._Process(double)，本次蛊手牌位置补丁未生效。"
            );
            return;
        }

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                extraHandProcess,
                postfix: new HarmonyMethod(typeof(GuHandLayoutPatch), nameof(ExtraHandProcessPostfix))
            );

            // 复位钩子只影响静态收起偏移的初始值，属于锦上添花：游戏内部方法
            // 缺失时跳过并记录警告，不影响布局主逻辑。
            TryPatchReset(harmony, "OnCombatEnded", [typeof(CombatRoom)]);
            TryPatchReset(harmony, nameof(NPlayerHand._ExitTree), Type.EmptyTypes);

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            GuHandLayoutSystem.Reset();
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            GuHandLayoutSystem.Reset();
            _initialized = false;
        }
    }

    // 只补丁 NPlayerHand 自己声明的方法；若该方法其实来自基类 Node，则放弃补丁，
    // 避免误挂到所有节点共用的虚方法上。
    private static void TryPatchReset(Harmony harmony, string methodName, Type[] parameters)
    {
        MethodInfo? target = AccessTools.DeclaredMethod(typeof(NPlayerHand), methodName, parameters);
        if (target == null)
        {
            Entry.Logger.Warn(
                $"[蛊手牌布局] 未找到 NPlayerHand.{methodName}，跳过手牌位置复位钩子。"
            );
            return;
        }

        harmony.Patch(
            target,
            prefix: new HarmonyMethod(typeof(GuHandLayoutPatch), nameof(ResetLayoutPrefix))
        );
    }

    private static void ExtraHandProcessPostfix(NModExtraHand __instance)
    {
        GuHandLayoutSystem.UpdateExtraHandLayout(
            __instance,
            Math.Max(0d, __instance.GetProcessDeltaTime())
        );
    }

    private static void ResetLayoutPrefix()
    {
        GuHandLayoutSystem.Reset();
    }
}
