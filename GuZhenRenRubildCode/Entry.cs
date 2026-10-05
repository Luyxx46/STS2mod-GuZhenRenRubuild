using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Combat;
using GuZhenRenRubild.Patches;
using GuZhenRenRubild.Ui;
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
        // 蛊牌费用区把原生能量费用改写成本蛊声明的元气点数，与元气系统同属"元气显示层"。
        // 该补丁只读 IGuCard.YuanQiCost 与本地 NCard 节点，不依赖元气资源编号，
        // 因此没有硬性先后要求，放在这里只是为了让同一层的组件相邻、便于排查。
        new(nameof(NCardGuEnergyCostPatch), NCardGuEnergyCostPatch.Initialize, NCardGuEnergyCostPatch.Uninitialize),
        // 空窍运行时负责转数与修为的跨存档/联机持久化，必须早于依赖它的遗物钩子就绪。
        new(nameof(ApertureSystem), ApertureSystem.Initialize, ApertureSystem.Uninitialize),
        // 杀招推演与材料绑定依赖牌堆注册（蛊封存区）与元气副资源，必须晚于二者启动。
        new(nameof(ShaZhaoTuiYanSystem), ShaZhaoTuiYanSystem.Initialize, ShaZhaoTuiYanSystem.Uninitialize),
        new(nameof(ShaZhaoBindingPatch), ShaZhaoBindingPatch.Initialize, ShaZhaoBindingPatch.Uninitialize),
        new(nameof(CompanionDeckLifecyclePatch), CompanionDeckLifecyclePatch.Initialize, CompanionDeckLifecyclePatch.Uninitialize),
        new(nameof(CompanionMutationProtectionPatch), CompanionMutationProtectionPatch.Initialize, CompanionMutationProtectionPatch.Uninitialize),
        new(nameof(GuCardRewardPatch), GuCardRewardPatch.Initialize, GuCardRewardPatch.Uninitialize),
        new(nameof(GuRankRewardPatch), GuRankRewardPatch.Initialize, GuRankRewardPatch.Uninitialize),
        // 仙蛊唯一性补丁只挂在 Hook.ShouldAddToDeck 上，读取奖励赋阶与升炼写下的
        // 转数/成仙登记，因此必须晚于 GuRankRewardPatch 启动。
        new(nameof(XianGuUniquenessPatch), XianGuUniquenessPatch.Initialize, XianGuUniquenessPatch.Uninitialize),
        new(nameof(GuRankUpPreviewPatch), GuRankUpPreviewPatch.Initialize, GuRankUpPreviewPatch.Uninitialize),
        new(nameof(GuCombatPatch), GuCombatPatch.Initialize, GuCombatPatch.Uninitialize),
        // 蛊手牌布局补丁只依赖牌堆注册结果，必须晚于 GuCardPileSystem 启动。
        new(nameof(GuHandLayoutPatch), GuHandLayoutPatch.Initialize, GuHandLayoutPatch.Uninitialize),
        // 仙元不足提示补丁只读 IGuCard.GuRank 与仙元余额、改写原版一句人物台词，
        // 不依赖任何已启动组件的状态，也没有硬性先后要求；放在这里只是让"提示层"组件相邻。
        new(nameof(XianYuanWarningPatch), XianYuanWarningPatch.Initialize, XianYuanWarningPatch.Uninitialize),
        // —— 原版适配裁决落地（Docs/原版适配裁决表_蛊手牌系统.xlsx，已裁决项）——
        // D-01：原版/事件升级作用于蛊牌 ⇒ 升 1 转（资格在 AbstractGuCard.MaxUpgradeLevel）。
        // 与 GuRankUpPreviewPatch 同挂 CardModel.UpgradeInternal，靠 Harmony 优先级分工；
        // 初始化只挂补丁不读状态，运行期读取的组件（牌堆/规则层）都已就绪。
        new(nameof(GuVanillaUpgradePatch), GuVanillaUpgradePatch.Initialize, GuVanillaUpgradePatch.Uninitialize),
        // D-05/D-06：拒绝原版附魔挂到蛊牌（伴生牌/普通牌不变）。
        new(nameof(GuEnchantGuardPatch), GuEnchantGuardPatch.Initialize, GuEnchantGuardPatch.Uninitialize),
        // D-09：原版苦难只允许命中激活区（蛊手牌）里的蛊牌，休眠堆免疫。
        new(nameof(GuAfflictionScopePatch), GuAfflictionScopePatch.Initialize, GuAfflictionScopePatch.Uninitialize),
        // D-04：原版变形产出的蛊牌补赋初始转数（含仙蛊封顶与成仙登记）。
        new(nameof(GuTransformRankPatch), GuTransformRankPatch.Initialize, GuTransformRankPatch.Uninitialize),
        // D-10：永久入组兜底补抽初始转数。挂点与仙蛊仲裁相同（Hook.ShouldAddToDeck），
        // 但运行期优先级更靠后，只给仲裁放行的牌赋阶；初始化只挂补丁不读状态。
        new(nameof(GuDeckEntryRankPatch), GuDeckEntryRankPatch.Initialize, GuDeckEntryRankPatch.Uninitialize),
        // D-16：烟雾/昏眩之力阻断蛊牌时的人物气泡提示（改写 combat_messages 台词，
        // 参照 XianYuanWarningPatch；与它分别接管 BlockedByHook 与 BlockedByCardLogic，互不冲突）。
        new(nameof(GuAfflictionWarningPatch), GuAfflictionWarningPatch.Initialize, GuAfflictionWarningPatch.Uninitialize),
        // D-14：玩家死亡时把 4 个蛊牌堆并入原版死亡清理（HandlePlayerDeath 只清五个原生堆）。
        new(nameof(GuDeathCleanupPatch), GuDeathCleanupPatch.Initialize, GuDeathCleanupPatch.Uninitialize),
        // 配方大全只读配方注册表，卡牌扫描在 RegisterContentOnce 中已完成，放在最后即可。
        new(nameof(RecipeCompendiumSystem), RecipeCompendiumSystem.Initialize, RecipeCompendiumSystem.Uninitialize),
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
        // 强化（AbstractCompanionEnhancement 的具体子类）登记进前置 MultiEnchantmentMod：
        // 由它把强化放进旁路额外附魔槽，从而与原版主槽附魔共存。
        CompanionEnhancementRegistration.Register();
        _contentRegistered = true;
    }

    // 用轻量只读记录统一保存组件名称、初始化委托和反初始化委托，便于循环处理和异常回滚。
    private readonly record struct RuntimeComponent(
        string Name,
        Action Initialize,
        Action Uninitialize
    );
}
