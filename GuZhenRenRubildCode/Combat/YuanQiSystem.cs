using Godot;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
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
    public const string LargeIconPath =
        $"res://{Entry.ModId}/images/characters/energy_big.png";
    public const string SmallIconPath =
        $"res://{Entry.ModId}/images/characters/energy_text.png";

    // 元气计数器的尺寸、图标、字体和增长动画样式。
    private static readonly SecondaryResourceCounterStyle CounterStyle =
        SecondaryResourceCounterStyle.Default with
        {
            CounterSize = new Vector2(52f, 52f),
            IconSize = new Vector2(46f, 46f),
            FontSize = 20,
            OutlineSize = 4,
            AnimateAmountGain = true,
        };

    // 资源注册只能执行一次，锁用于防止不同初始化入口并发注册。
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    // 返回包含模组命名空间的完整资源编号，所有读写元气的命令都使用该编号。
    public static string ResourceId =>
        ModSecondaryResourceRegistry.GetResourceId(Entry.ModId, LocalId);

    // 元气默认值为 0、上限为 5，不由框架自动在回合开始恢复，并且只在当前战斗内持久化。
    // 文本标题、描述和大小图标均通过本模组资源路径与本地化键提供。
    public static SecondaryResourceDefinition Definition { get; private set; } =
        new(
            defaultAmount: 0,
            baseMaxAmount: 5,
            minAmount: 0,
            hardMaxAmount: 5,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            locTable: "secondary_resources",
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
            registry.RegisterCombatUi<NSecondaryResourceCounter>(
                LocalId,
                static _ => NSecondaryResourceCounter.Create(Definition, CounterStyle),
                static context =>
                {
                    // 节点创建后先绑定对应玩家，再移动到原生能量计数器旁边。
                    context.Node.Bind(context.Player);
                    AttachBesideEnergy(context.Node, context.Parent);
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

    // 优先把元气计数器挂到原生能量计数器节点下；如果未找到原生计数器，则退回能量容器作为锚点。
    private static void AttachBesideEnergy(
        NSecondaryResourceCounter counter,
        NCombatUi combatUi
    )
    {
        NEnergyCounter? nativeCounter = combatUi.EnergyCounterContainer
            .GetChildren()
            .OfType<NEnergyCounter>()
            .FirstOrDefault();
        Node anchor = nativeCounter ?? combatUi.EnergyCounterContainer;

        // 仅在父节点确实不一致时重挂，避免无意义的节点树操作。
        if (!ReferenceEquals(counter.GetParent(), anchor))
        {
            counter.Reparent(anchor, keepGlobalTransform: false);
        }

        // 使用固定偏移把元气计数器放到原生能量球右上侧，避免与原界面重叠。
        counter.Position = new Vector2(96f, -102f);
    }
}
