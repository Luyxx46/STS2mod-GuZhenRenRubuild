using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;

// =========================================================================
//  聚光生产强化（汇光 / 借辉 / 璇照 / 月王辉强化）
//
//  这一组强化在“折光真正触发”时额外给出聚光。它们挂在纯功能折光或
//  数值折光上都成立：纯功能折光不会因为这份追加而变成数值型折光。
// =========================================================================

/// <summary>
/// 【汇光】：父蛊 <c>聚光蛊</c> 催动时给 <c>聚光镜</c> 挂 2 次。
/// 聚光镜基础折光给 1 聚光，汇光再追加 1 层（因此该次共得 2 聚光）；
/// 本场最多触发 2 次，也就是最多额外制造 2 次重复机会。
/// </summary>
[RegisterEnchantment]
public sealed class HuiGuangEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 2;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public int ExtraJuGuangOnRefraction => GrantedMagnitude;
}

/// <summary>
/// 【借辉】：父蛊 <c>借光蛊</c> 催动时给 <c>借光束</c> 挂 2 次。
/// 借光束基础折光只在「当前聚光为 0」时补 1 层；借辉把这两次改成
/// <b>无论当前是否已有聚光都获得 1 聚光</b>，用于断档续接而不是大量囤积。
/// </summary>
[RegisterEnchantment]
public sealed class JieHuiEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 2;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public int ExtraJuGuangOnRefraction => GrantedMagnitude;
}

/// <summary>
/// 【璇照】：父蛊 <c>璇光蛊</c> 催动时给 <c>璇光刃</c> 与 <c>璇光幕</c> 各挂 1 次，
/// 成功折光后额外获得 1 聚光；每张本场最多触发 1 次。
///
/// <para>
/// 两张伴生自身的折光段是纯功能的「抽 1 张牌」，因此抽牌不会被聚光复制，
/// 只有这份聚光会被后续数值折光消费。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class XuanZhaoEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public int ExtraJuGuangOnRefraction => GrantedMagnitude;
}

/// <summary>
/// 【月王辉强化】：父蛊 <c>宝月光王蛊</c> 催动时给 <c>月王辉</c> 挂 1 次。
/// 月王辉基础折光给 1 聚光并使下一张攻击牌强制折光；强化后该次共获得 2 聚光。
/// </summary>
[RegisterEnchantment]
public sealed class YueWangHuiEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public int ExtraJuGuangOnRefraction => GrantedMagnitude;
}
