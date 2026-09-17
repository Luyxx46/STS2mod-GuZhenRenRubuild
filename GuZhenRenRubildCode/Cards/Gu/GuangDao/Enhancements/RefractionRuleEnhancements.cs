using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;

// =========================================================================
//  折光规则强化（虹行 / 太照 / 恒照 / 回照强化）
//
//  这一组强化改变的是折光的“发生方式”或“重复之后的收益”，
//  而不是折光段的数值。
// =========================================================================

/// <summary>
/// 【虹行】：父蛊 <c>光虹蛊</c> 催动时给两张 <c>虹步</c> 各挂 1 次。
/// 虹步成功折光后，使下一张拥有折光段的牌强制折光；每张本场最多触发 1 次。
///
/// <para>
/// 虹步自身的折光段是纯功能的抽牌，所以虹行属于纯功能折光的附加值：
/// 既不消耗聚光，也不会被聚光重复。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class HongXingEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public bool ForcesNextRefraction => true;
}

/// <summary>
/// 【太照】：父蛊 <c>太光蛊</c> 催动时给 <c>太光贯</c> 挂 1 次。
/// 下一次它的折光<b>因聚光发生重复</b>时，重复出来的那第二遍折光伤害额外 +4；
/// 本场只强化一次。
///
/// <para>
/// 因此强化爆发为 8 基础 + 6 第一次折光 + 10 重复折光 = 24 点。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class TaiZhaoEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    // 只加在重复出来的那一遍（passIndex >= 1）。
    public decimal RefractionDamageBonus(int passIndex) =>
        passIndex >= 1 ? GrantedMagnitude : 0m;
}

/// <summary>
/// 【恒照】：父蛊 <c>恒光仙蛊</c> 催动时给 <c>恒光刃</c> 与 <c>恒光障</c> 挂上。
/// 若本牌本次折光因聚光发生重复，则重复结算后获得 1 聚光，
/// 并使下一张拥有折光段的牌强制折光。
///
/// <para>
/// 每张伴生的可用次数按转数授予：六转 1 次、七转 2 次、八转 3 次
/// （由父蛊通过 <see cref="CompanionEnhancementGrant.UsesPerCombat"/> 覆写）。
/// 返还只发生在恒光伴生身上且有次数上限，因此不会让全牌组永久免费重复。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class HengZhaoEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public bool RewardsRepeatedRefraction => true;
}

/// <summary>
/// 【回照强化】：父蛊 <c>回光蛊</c> 催动时给 <c>回照</c> 挂上。
/// 被回收的攻击伴生牌本回合费用 -1（四转起变为 0 费）。
///
/// <para>
/// 回照自身的折光段是纯功能的回收，因此这份减费既不被聚光复制、
/// 也不会让回收动作本身消耗聚光。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class HuiZhaoEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public int RecoverCostReduction => GrantedMagnitude;
}
