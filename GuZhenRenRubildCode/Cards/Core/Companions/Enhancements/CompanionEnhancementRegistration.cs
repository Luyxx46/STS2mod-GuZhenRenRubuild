using System.Reflection;

using MultiEnchantmentMod;
using MultiEnchantmentMod.Api;

// 声明本程序集构建时依赖的 MultiEnchantmentMod 公共 API 版本：
// 缺失该特性时框架会告警（分析器 MEM007），声明版本高于框架当前版本时框架拒绝扫描本程序集。
[assembly: EnchantmentApiCompatibility(MultiEnchantmentApiVersion.Current)]

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 把本模组的强化类型登记进 <b>MultiEnchantmentMod</b>（本模组前置），使其获得「同名叠层」语义。
///
/// <para>
/// 采用<b>集中式显式登记</b>而不是给每个强化子类挂 <c>[Enchantment]</c> 特性，理由有两条：
/// <list type="bullet">
/// <item>MultiEnchantmentMod 的 <c>[Enchantment]</c> 是 <c>Inherited = false</c> 的类特性，
/// 挂在 <see cref="AbstractCompanionEnhancement"/> 上<b>不会</b>被子类继承，逐个子类标注必漏。</item>
/// <item>漏标注的后果是静默的行为退化：框架会在首次使用时按启发式把该类型自动登记成
/// <c>DisallowDuplicate</c>，同一强化被重复催动时不再叠层，且不报错。</item>
/// </list>
/// 因此这里在初始化时扫描程序集内全部具体强化类型，统一按
/// <see cref="StackBehavior.MergeAmount"/> + <see cref="StatusAggregation.SharedAcrossStack"/> 登记
/// ——语义等价于旧实现「一个强化槽 + 层数」：一个强化类型在卡上只有一个实例，层数记在
/// <c>Amount</c> 上并显示为图标数量。同时把 <c>OnPlay</c> 的执行策略改成
/// <see cref="HookExecutionMode.PerLiveInstance"/>：<c>MergeAmount</c> 默认是
/// <c>MergedTotal</c>（按层数各跑一次），会让「打出一次子卡」扣掉与层数相同的可用次数。
/// 新增具体强化子类<b>无需</b>改本文件。
/// </para>
///
/// <para>
/// 调用时机为 <c>Entry.RegisterContentOnce</c>（RitsuLib 内容登记之后）。模组清单已声明对
/// MultiEnchantmentMod 的依赖，故其注册表此时可用；若 API 版本不匹配，则只记警告并跳过登记，
/// 不抛异常打断整个模组的初始化（后续挂载会由框架拒绝并留下日志）。
/// </para>
/// </summary>
internal static class CompanionEnhancementRegistration
{
    // 登记句柄：Commit 返回的 IDisposable 可撤销登记，这里保留引用以便将来支持热重载/退场。
    private static readonly List<IDisposable> Handles = [];

    private static bool _registered;

    /// <summary>幂等登记；重复调用只生效一次。</summary>
    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        // 版本不匹配时框架自己会记一条错误日志，这里再补一条本模组视角的说明后直接返回。
        if (!MultiEnchantmentApi.RequireApiVersion(MultiEnchantmentApiVersion.Current))
        {
            Entry.Logger.Warn(
                "[Companion/Enhancement] action=SkipRegistration " +
                $"reason=MultiEnchantmentModApiVersionMismatch " +
                $"required={MultiEnchantmentApiVersion.Current}"
            );
            return;
        }

        int count = 0;
        foreach (Type type in EnumerateEnhancementTypes())
        {
            Handles.Add(
                MultiEnchantmentApi
                    .Register(type)
                    .Stack(StackBehavior.MergeAmount, StatusAggregation.SharedAcrossStack)
                    // MergeAmount 的默认执行策略是 MergedTotal：OnPlay 会按层数各跑一次，
                    // 那样「打出一次子卡」会一次扣掉与层数相同的可用次数。改为每实例一次。
                    .Execution(p => p.OnPlay(HookExecutionMode.PerLiveInstance))
                    .Commit()
            );
            count++;
        }

        Entry.Logger.Info(
            $"[Companion/Enhancement] Registered {count} enhancement type(s) " +
            "with MultiEnchantmentMod (MergeAmount/SharedAcrossStack)."
        );
    }

    /// <summary>程序集内全部具体强化类型；抽象父类、接口与结构体不参与登记。</summary>
    private static IEnumerable<Type> EnumerateEnhancementTypes()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            if (typeof(AbstractCompanionEnhancement).IsAssignableFrom(type))
            {
                yield return type;
            }
        }
    }
}
