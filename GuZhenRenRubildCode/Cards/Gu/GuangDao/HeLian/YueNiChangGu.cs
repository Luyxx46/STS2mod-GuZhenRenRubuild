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
/// 月霓裳蛊（二转 · 月系旁系第一次质变）。
///
/// <para>
/// 合练：月光蛊 + 玉皮蛊，结果转数 = 材料最高转数 + 1 = 2。
/// 合练前的两张低转材料蛊离场，改由两只 <see cref="YueNi"/> 承接防御动作。
/// </para>
///
/// <para>
/// B 形态纯增幅：催动给两张月霓各挂 1 次【霓护】，本次折光格挡额外 +2。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(global::GuZhenRenRubild.Cards.Basic.YueGuangGu.YueGuangGu),
    typeof(global::GuZhenRenRubild.Cards.Basic.YuPiGu.YuPiGu),
    MinimumMaterialRank = 1
)]
public sealed class YueNiChangGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>二转单点窗口。</summary>
    public override int MaxGuRank => 2;

    /// <summary>月系低阶蛊只需 1 点元气。</summary>
    public override int YuanQiCost => 1;

    public CompanionDefinition Companion => new(typeof(YueNi), 2);

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<NiHuEnhancement>(
                amount: 1,
                magnitude: 2,
                usesPerCombat: 1
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public YueNiChangGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
