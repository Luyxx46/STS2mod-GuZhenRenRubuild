using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Characters;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace GuZhenRenRubild.Combat;

/// <summary>
/// 元气系统：一种只在战斗中存在的副资源，专门用于支付蛊牌的激活费用。
/// 负责资源定义、战斗界面计数器注册以及计数器在原生能量区域旁的布局。
/// </summary>
public static class YuanQiSystem
{
    // LocalId 用于模组内部注册；ResourceId 会由 RitsuLib 加上模组命名空间生成全局唯一编号。
    public const string LocalId = "yuan_qi";

    // 元气图标就是角色能量图标，路径统一由视觉资源常量提供。
    public const string LargeIconPath =
        GuZhenRenRubildAssets.BigEnergyIconPath;
    public const string SmallIconPath =
        GuZhenRenRubildAssets.TextEnergyIconPath;

    // 元气转盘使用角色专属场景显示；场景缺失时桥接类会退回程序化图标界面。
    public const string SecondaryCounterScenePath =
        $"res://{Entry.ModId}/scenes/ui/nodes/GuZhenRenRubild_yuanqi_counter.tscn";

    // 资源注册只能执行一次，锁用于防止不同初始化入口并发注册。
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    // 返回包含模组命名空间的完整资源编号，所有读写元气的命令都使用该编号。
    public static string ResourceId =>
        ModSecondaryResourceRegistry.GetResourceId(Entry.ModId, LocalId);

    // 元气默认值为 0，不由框架自动在回合开始恢复，并且只在当前战斗内持久化。
    // 上限由空窍遗物按当前转数改写（一转 3 ~ 九转 9）；hardMaxAmount 取已实现曲线的
    // 最大容量（九转），保证即使改写钩子缺席也不会超出该曲线的最大值。
    // 文本标题、描述和大小图标均通过本模组资源路径与本地化键提供。
    public static SecondaryResourceDefinition Definition { get; private set; } =
        new(
            defaultAmount: 0,
            baseMaxAmount: 5,
            minAmount: 0,
            hardMaxAmount: ApertureProgression.MaximumYuanQiCapacity,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            // 悬浮提示文本必须放在"与原版同名"的表里：游戏只会合并这类模组本地化表
            // （godot.log 中的 "Found loc table from mod: zhs xxx.json"），自建表名
            // 不会被加载，键名会原样显示在提示框里。static_hover_tips 是原版提示表，
            // RitsuLib 的牌堆提示与本模组的其余提示键也都在这里。
            locTable: "static_hover_tips",
            titleKey: "GU_ZHEN_REN_RUBILD_SECONDARY_RESOURCE_YUAN_QI.title",
            descriptionKey: "GU_ZHEN_REN_RUBILD_SECONDARY_RESOURCE_YUAN_QI.description",
            smallIconPath: SmallIconPath,
            largeIconPath: LargeIconPath
        );

    // 注册元气资源本体与战斗 UI，并指定本角色进入战斗时始终显示元气计数器。
    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            ModSecondaryResourceRegistry registry =
                ModSecondaryResourceRegistry.For(Entry.ModId);
            // Register 可能返回框架规范化后的定义，因此用返回值覆盖本地缓存。
            Definition = registry.Register(LocalId, Definition);
            registry.RegisterCombatUi<YuanQiEnergyCounter>(
                LocalId,
                static _ => YuanQiEnergyCounter.Create(
                    Definition,
                    SecondaryCounterScenePath
                ),
                static context =>
                {
                    // 节点创建后先绑定对应玩家，再移动到原生能量计数器旁边。
                    context.Node.Bind(context.Player);

                    // 节点挂载注册会作用于所有角色的战斗界面；只有本模组
                    // 角色才把元气表定位到原生能量表右上方，避免影响其他角色。
                    if (context.Player?.Character is GuZhenRenRubildCharacter)
                    {
                        context.Node.AttachBesideNativeEnergyCounter(
                            context.Parent
                        );
                    }
                },
                static context => context.Node.Refresh(context.Player),
                new NodeAttachmentOptions
                {
                    Name = "YuanQiCounter",
                    IncludeDerivedParentTypes = true,
                }
            );
            registry.AlwaysShowInCombatUiForCharacter<GuZhenRenRubildCharacter>(
                LocalId
            );
            _initialized = true;
        }
    }

    public static void Uninitialize()
    {
        // RitsuLib 的副资源与界面注册在整个进程内有效，目前不需要也不应重复反注册。
    }
}
