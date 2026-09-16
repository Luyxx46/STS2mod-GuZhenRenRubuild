using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Basic.YueGuangGu;

/// <summary>
/// 「月刃强化」：父蛊 <see cref="YueGuangGu"/>（月光蛊）每次催动时，给它的子卡
/// <see cref="YueGuangCompanion"/>（月刃）叠 1 层。
///
/// <para>
/// 每层使月刃**打出的伤害 +2**（层数即 <c>Amount</c>，上限 <see cref="MaxAmount"/>）。
/// 只作用于「有力量加成的攻击伤害」（<c>IsPoweredAttack</c>），与同目录伴生牌
/// <see cref="YueGuangCompanion"/> 的 <c>DamageVar(Move)</c> 一致。
/// </para>
///
/// <para>
/// <b>可用次数</b>：每场战斗 3 次（<see cref="UsesPerCombat"/>）。每次月刃打出并结算后消耗 1 次，
/// 3 次用尽即整项清空（卡面强化图标消失），本场不再接受重新授予；下一场战斗需由
/// <see cref="YueGuangGu"/> 重新催动授予。
/// </para>
///
/// <para>
/// 注意原版钩子的契约：<c>EnchantDamageAdditive</c> 返回的是**增量**而不是「原值 + 增量」
/// ——前置分发侧是 <c>result += 钩子(...)</c>，返回原值+增量会双倍计算。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class YueRenEnhancement : AbstractCompanionEnhancement
{
    // 每层提升的伤害；数值改动只需动这一处常量。
    private const decimal DamagePerStack = 2m;

    // 强化层数上限：三次催动封顶（服务层挂载时会按此上限夹取层数）。
    public override int MaxAmount => 3;

    // 每场战斗可用次数：打出 3 次月刃后本强化清空。数值改动只需动这一处常量。
    public override int UsesPerCombat => 3;

    // 卡面追加一行说明，让玩家在子卡上直接看到强化效果。
    public override bool HasExtraCardText => true;

    public override decimal EnchantDamageAdditive(
        decimal originalDamage,
        ValueProp props
    ) => props.IsPoweredAttack() ? DamagePerStack * Amount : 0m;
}
