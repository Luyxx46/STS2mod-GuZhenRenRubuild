using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Combat;
using GuZhenRenRubild.Patches;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace GuZhenRenRubild;

// 模组入口类。ModInitializer 会让游戏加载模组时调用 Initialize。
[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    // 模组编号同时用于注册命名空间和 Godot 资源根路径，必须与资源目录保持一致。
    public const string ModId = "GuZhenRenRubild";
    public const string ResPath = $"res://{ModId}";

    // 全局日志器供初始化、运行时系统和异常回滚共同使用。
    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    // 初始化可能由多个入口触发，因此使用锁和状态标记保证整个过程只执行一次。
    private static readonly object InitializationLock = new();
    // 按依赖顺序登记所有运行时组件；初始化失败时会按相反顺序逐个回滚。
    private static readonly RuntimeComponent[] RuntimeComponents =
    [
        new(nameof(GuCardPileSystem), GuCardPileSystem.Initialize, GuCardPileSystem.Uninitialize),
        new(nameof(YuanQiSystem), YuanQiSystem.Initialize, YuanQiSystem.Uninitialize),
        // 空窍运行时负责转数与修为的跨存档/联机持久化，必须早于依赖它的遗物钩子就绪。
        new(nameof(ApertureSystem), ApertureSystem.Initialize, ApertureSystem.Uninitialize),
        // 杀招推演与材料绑定依赖牌堆注册（蛊封存区）与元气副资源，必须晚于二者启动。
        new(nameof(ShaZhaoTuiYanSystem), ShaZhaoTuiYanSystem.Initialize, ShaZhaoTuiYanSystem.Uninitialize),
        new(nameof(ShaZhaoBindingPatch), ShaZhaoBindingPatch.Initialize, ShaZhaoBindingPatch.Uninitialize),
        new(nameof(CompanionDeckLifecyclePatch), CompanionDeckLifecyclePatch.Initialize, CompanionDeckLifecyclePatch.Uninitialize),
        new(nameof(CompanionMutationProtectionPatch), CompanionMutationProtectionPatch.Initialize, CompanionMutationProtectionPatch.Uninitialize),
        new(nameof(GuCardRewardPatch), GuCardRewardPatch.Initialize, GuCardRewardPatch.Uninitialize),
        new(nameof(GuRankRewardPatch), GuRankRewardPatch.Initialize, GuRankRewardPatch.Uninitialize),
        new(nameof(GuRankUpPreviewPatch), GuRankUpPreviewPatch.Initialize, GuRankUpPreviewPatch.Uninitialize),
        new(nameof(GuCombatPatch), GuCombatPatch.Initialize, GuCombatPatch.Uninitialize),
        // 蛊手牌布局补丁只依赖牌堆注册结果，必须晚于 GuCardPileSystem 启动。
        new(nameof(GuHandLayoutPatch), GuHandLayoutPatch.Initialize, GuHandLayoutPatch.Uninitialize),
    ];

    private static bool _contentRegistered;
    private static bool _initialized;

    // 先注册静态内容，再启动牌堆、元气和 Harmony 补丁。整个过程按事务式思路处理，任一组件失败都会回滚已启动组件。
    public static void Initialize()
    {
        lock (InitializationLock)
        {
            if (_initialized)
            {
                return;
            }

            RegisterContentOnce();

            // 记录已经成功初始化的组件数量，异常时只回滚真正启动过的部分。
            int initializedCount = 0;
            try
            {
                foreach (RuntimeComponent component in RuntimeComponents)
                {
                    component.Initialize();
                    initializedCount++;
                }

                _initialized = true;
                Logger.Info("Gu card core initialized.");
            }
            catch
            {
                // 按初始化的逆序回滚，尽量恢复到调用 Initialize 之前的状态；单个回滚失败只记录日志，不覆盖原始异常。
                for (int index = initializedCount - 1; index >= 0; index--)
                {
                    try
                    {
                        RuntimeComponents[index].Uninitialize();
                    }
                    catch (Exception rollbackException)
                    {
                        Logger.Warn(
                            $"Failed to roll back {RuntimeComponents[index].Name}: " +
                            rollbackException.Message
                        );
                    }
                }

                throw;
            }
        }
    }

    // 自动注册当前程序集中的 Godot 脚本与 RitsuLib 内容类型；该步骤只需要执行一次。
    private static void RegisterContentOnce()
    {
        if (_contentRegistered)
        {
            return;
        }

        Assembly assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        _contentRegistered = true;
    }

    // 用轻量只读记录统一保存组件名称、初始化委托和反初始化委托，便于循环处理和异常回滚。
    private readonly record struct RuntimeComponent(
        string Name,
        Action Initialize,
        Action Uninitialize
    );
}
