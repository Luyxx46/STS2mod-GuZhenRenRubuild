using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Basic.YueGuangGu;

/// <summary>
/// 【月华】：父蛊 <see cref="YueGuangGu"/>（月光蛊）每次催动时，给它的子卡
/// <see cref="YueGuangCompanion"/>（月刃）叠 1 层。
///
/// <para>
/// 每层使月刃的<b>折光段伤害 +2</b>（层数即 <c>Amount</c>，上限 <see cref="MaxAmount"/>）。
/// 注意它加的是折光段而非基础伤害：折光段被聚光重复时这份加成也会一起再结算一次。
/// </para>
///
/// <para>
/// <b>可用次数</b>：每场战斗 3 次（<see cref="UsesPerCombat"/>）。只有月刃<b>真正折光</b>的那次
/// 出牌才消耗 1 次（<see cref="RequiresRefractionTrigger"/>），3 次用尽即整项清空；
/// 下一场战斗需由月光蛊重新催动授予。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class YueHuaEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    // 每层提升的折光伤害；数值改动只需动这一处常量。
    private const decimal RefractionDamagePerStack = 2m;

    // 强化层数上限：三次催动封顶（服务层挂载时会按此上限夹取层数）。
    public override int MaxAmount => 3;

    // 每场战斗可用次数：月刃折光 3 次后本强化清空。
    public override int UsesPerCombat => 3;

    // 卡面追加一行说明，让玩家在子卡上直接看到强化效果。
    public override bool HasExtraCardText => true;

    // 只有真正折光才扣次数：打出但没折光的月刃不吃次数。
    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionDamageBonus(int passIndex) =>
        RefractionDamagePerStack * Amount;
}
