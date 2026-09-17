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
/// 皓月霓裳蛊（三转 · 月系旁系）。
///
/// <para>
/// 合练：月霓裳蛊 + 反光蛊（两张材料均需至少 2 转），结果转数 = 3。
/// 主防御动作 <see cref="HaoYueShou"/> 扛数值折光，<see cref="HaoYueYin"/> 负责把折光接续给攻击侧。
/// </para>
///
/// <para>
/// B 形态纯增幅：催动给两张伴生各挂 1 次【皓辉】，其数值型折光格挡额外 +2。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(YueNiChangGu),
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.FanGuangGu),
    MinimumMaterialRank = 2
)]
public sealed class HaoYueNiChangGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>三转单点窗口。</summary>
    public override int MaxGuRank => 3;

    /// <summary>月系低阶蛊只需 1 点元气。</summary>
    public override int YuanQiCost => 1;

    public CompanionDefinition Companion => new(typeof(HaoYueShou));

    public IReadOnlyList<CompanionDefinition> CompanionDefinitions =>
        [new(typeof(HaoYueShou)), new(typeof(HaoYueYin))];

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<HaoHuiEnhancement>(
                amount: 1,
                magnitude: 2,
                usesPerCombat: 1
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public HaoYueNiChangGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
