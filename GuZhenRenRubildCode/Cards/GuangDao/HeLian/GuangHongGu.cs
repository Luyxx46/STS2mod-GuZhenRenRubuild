using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Gu.GuangDao;
using GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.HeLian;

/// <summary>
/// 光虹蛊（三转 · 光道主线）。
///
/// <para>
/// 合练：辉光蛊 + 炫光蛊（两张材料均需至少 2 转），结果转数 = 材料最高转数 + 1 = 3。
/// </para>
///
/// <para>
/// B 形态纯增幅：把攻击密度转为技能循环——催动给两张 <see cref="HongBu"/> 各挂 1 次【虹行】，
/// 成功折光后使下一张拥有折光段的牌强制折光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.HuiGuangGu),
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.XuanGuangGu),
    MinimumMaterialRank = 2
)]
public sealed class GuangHongGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>三转单点窗口。</summary>
    public override int MaxGuRank => 3;

    public CompanionDefinition Companion => new(typeof(HongBu), 2);

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<HongXingEnhancement>(
                amount: 1,
                magnitude: 1,
                usesPerCombat: 1
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public GuangHongGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
