namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 一次「催动蛊牌 → 强化伴生牌」的授予声明：强化类型 + 自定义层数。
///
/// <para>
/// 形态对齐原版自定义附魔次数的参照（<c>CardCmd.Enchant(enchantment, card, amount)</c> 与
/// <c>EnchantmentOption(enchantment, minAmount, maxAmount)</c>）：层数由授予方按需给定，
/// 实际落卡时统一夹进 <c>[1, <see cref="AbstractCompanionEnhancement.MaxAmount"/>]</c>。
/// 声明只描述意图，不携带任何运行时实例；真正的挂载统一走
/// <see cref="CompanionEnhancementService"/>，与手动挂载共用同一条写入链
/// （同类型叠层、一卡一项强化、与原版主槽附魔共存）。
/// </para>
/// </summary>
/// <param name="EnhancementType">
/// 强化类型，必须是 <see cref="AbstractCompanionEnhancement"/> 的已注册子类。
/// </param>
/// <param name="Amount">
/// 本次授予的层数；最小按 1 处理，上限由强化自身的
/// <see cref="AbstractCompanionEnhancement.MaxAmount"/> 决定。
/// </param>
/// <param name="Magnitude">
/// 本次授予的<b>数值标量</b>（例如「折光格挡额外 +X」里的 X）。
/// 光道催动需要按转数给出不同数值，而层数只负责显示，两者必须分开；
/// <c>null</c> 表示用强化自身的默认值。
/// </param>
/// <param name="UsesPerCombat">
/// 本次授予的<b>每场战斗可用次数</b>（例如恒照按转数给 1/2/3 次）。
/// <c>null</c> 表示用强化自身声明的 <see cref="AbstractCompanionEnhancement.UsesPerCombat"/>。
/// </param>
/// <param name="CompanionTypes">
/// 只给这些类型的伴生牌挂载；<c>null</c> 表示给配对内的全部伴生牌。
/// 一只蛊的两种伴生需要不同强化时（例如宝月光王蛊）用它分流。
/// </param>
public sealed record CompanionEnhancementGrant(
    Type EnhancementType,
    int Amount = 1,
    int? Magnitude = null,
    int? UsesPerCombat = null,
    IReadOnlyList<Type>? CompanionTypes = null
)
{
    /// <summary>按强化类型与层数构造授予声明；泛型参数保证类型在编译期受约束。</summary>
    public static CompanionEnhancementGrant Of<TEnhancement>(
        int amount = 1,
        int? magnitude = null,
        int? usesPerCombat = null,
        IReadOnlyList<Type>? companionTypes = null
    )
        where TEnhancement : AbstractCompanionEnhancement
    {
        return new CompanionEnhancementGrant(
            typeof(TEnhancement),
            amount,
            magnitude,
            usesPerCombat,
            companionTypes
        );
    }

    /// <summary>该类型是否属于本次授予的目标伴生。</summary>
    internal bool Targets(Type companionType) =>
        CompanionTypes == null ||
        CompanionTypes.Contains(companionType);
}
