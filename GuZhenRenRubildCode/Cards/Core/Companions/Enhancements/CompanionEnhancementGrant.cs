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
public sealed record CompanionEnhancementGrant(Type EnhancementType, int Amount = 1)
{
    /// <summary>按强化类型与层数构造授予声明；泛型参数保证类型在编译期受约束。</summary>
    public static CompanionEnhancementGrant Of<TEnhancement>(int amount = 1)
        where TEnhancement : AbstractCompanionEnhancement
    {
        return new CompanionEnhancementGrant(typeof(TEnhancement), amount);
    }
}
