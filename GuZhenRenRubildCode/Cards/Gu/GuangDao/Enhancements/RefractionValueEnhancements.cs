using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;

// =========================================================================
//  折光段数值强化（辉映 / 炫光幕强化 / 引光强化 / 炽照 / 返照 / 霓护 / 皓辉 /
//  宝霓 / 宝月王衣强化）
//
//  这一组强化“抬高折光段的数值”。他们全部只在子卡真正折光时消耗次数，
//  并且数值走 GrantedMagnitude（由父蛊按转数授予）而不是层数，
//  因此层数只用于卡面显示，两条轴不会互相污染。
// =========================================================================

/// <summary>
/// 【辉映】：父蛊 <c>辉光蛊</c> 催动时给两张 <c>辉光矢</c> 各叠 <c>转数</c> 层。
/// 每层使辉光矢的<b>折光段伤害 +1</b>；最多 2 层，每张本场最多触发 2 次。
/// </summary>
[RegisterEnchantment]
public sealed class HuiYingEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 2;

    public override int UsesPerCombat => 2;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionDamageBonus(int passIndex) => Amount;
}

/// <summary>
/// 【炫光幕强化】：父蛊 <c>炫光蛊</c> 催动时给 <c>炫光幕</c> 挂上，
/// 本场前 2 次折光额外获得 <c>转数</c> 点格挡；折光段重复时这份加成同样再结算一次。
/// </summary>
[RegisterEnchantment]
public sealed class XuanGuangMuEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 2;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}

/// <summary>
/// 【引光强化】：父蛊 <c>光源蛊</c> 催动时给 <c>引光</c> 挂上，
/// 本场前 2 次折光额外获得 <c>转数</c> 点格挡。
///
/// <para>
/// 引光的基础折光段是纯功能的「回复 1 元气」，因此这份格挡只是<b>附加数值</b>，
/// 不会让引光变成数值型折光、也不会被聚光重复。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class YinGuangEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 2;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}

/// <summary>
/// 【炽照】：父蛊 <c>红光蛊</c> 催动时给两张 <c>赤光</c> 各挂 1 次，
/// 本次折光伤害额外 +<c>转数</c>；每张本场最多触发 1 次。
/// </summary>
[RegisterEnchantment]
public sealed class ChiZhaoEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionDamageBonus(int passIndex) => GrantedMagnitude;
}

/// <summary>
/// 【返照】：父蛊 <c>反光蛊</c> 催动时给两张 <c>反光壁</c> 各挂 1 次，
/// 本次折光格挡额外 +<c>转数</c>；每张本场最多触发 1 次。
/// </summary>
[RegisterEnchantment]
public sealed class FanZhaoEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}

/// <summary>
/// 【霓护】：父蛊 <c>月霓裳蛊</c> 催动时给两张 <c>月霓</c> 各挂 1 次，
/// 本次折光格挡额外 +2；每张本场最多触发 1 次。
/// </summary>
[RegisterEnchantment]
public sealed class NiHuEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}

/// <summary>
/// 【皓辉】：父蛊 <c>皓月霓裳蛊</c> 催动时给 <c>皓月绶</c> 与 <c>皓月引</c> 各挂 1 次，
/// 其数值型折光格挡额外 +2；每张本场最多触发 1 次。
/// </summary>
[RegisterEnchantment]
public sealed class HaoHuiEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}

/// <summary>
/// 【宝霓】：父蛊 <c>宝月霓裳蛊</c> 催动时给 <c>宝月衣</c> 挂 2 次，
/// 折光格挡额外 +3；本场最多触发 2 次。
/// </summary>
[RegisterEnchantment]
public sealed class BaoNiEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 2;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}

/// <summary>
/// 【宝月光王·衣】：父蛊 <c>宝月光王蛊</c> 催动时给 <c>宝月王衣</c> 挂 1 次，
/// 折光格挡额外 +4；本场最多触发 1 次。
/// </summary>
[RegisterEnchantment]
public sealed class BaoYueWangYiEnhancement
    : AbstractCompanionEnhancement,
      IGuangDaoCompanionEnhancement
{
    public override int MaxAmount => 1;

    public override int UsesPerCombat => 1;

    public override bool HasExtraCardText => true;

    protected override bool RequiresRefractionTrigger => true;

    public decimal RefractionBlockBonus => GrantedMagnitude;
}
