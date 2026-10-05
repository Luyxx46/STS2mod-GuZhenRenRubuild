using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.HeLian;

/// <summary>
/// 宝月霓裳蛊（四转 · 月系旁系）。
///
/// <para>
/// 合练：皓月霓裳蛊 + 回光蛊（两张材料均需至少 3 转），结果转数 = 4。
/// 动作收束为单张高质量防御牌 <see cref="BaoYueYi"/>。
/// </para>
///
/// <para>
/// B 形态纯增幅：催动给宝月衣挂 2 次【宝霓】，折光格挡额外 +3（本场最多触发 2 次）。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(HaoYueNiChangGu),
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.HuiGuangBackGu),
    MinimumMaterialRank = 3
)]
public sealed class BaoYueNiChangGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>四转单点窗口。</summary>
    public override int MaxGuRank => 4;

    /// <summary>四转月系蛊催动需要 2 点元气。</summary>
    public override int YuanQiCost => 2;

    /// <summary>四转月系蛊冷却 3 回合。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(BaoYueYi));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<BaoNiEnhancement>(
                amount: 1,
                magnitude: 3,
                usesPerCombat: 2
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public BaoYueNiChangGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }
}
