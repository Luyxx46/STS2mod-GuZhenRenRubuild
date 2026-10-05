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
/// 宝月光王蛊（五转 · 月系旁系终点，六转并流接口）。
///
/// <para>
/// 合练：宝月霓裳蛊 + 璇光蛊（两张材料均需至少 4 转），结果转数 = 5。
/// </para>
///
/// <para>
/// B 形态纯增幅，两条声明分别落到两张不同的伴生上：
/// <list type="bullet">
/// <item><see cref="BaoYueWangYi"/> ←【宝月光王·衣】：折光格挡额外 +4（本场 1 次）；</item>
/// <item><see cref="YueWangHui"/> ←【月王辉强化】：该次折光额外获得 1 聚光（总计 2）。</item>
/// </list>
/// 于是宝月光王可以先用月王辉准备「强制折光 + 聚光」，
/// 再把重复机会交给攻击侧的高价值折光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(BaoYueNiChangGu),
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.XuanGuangXuanGu),
    MinimumMaterialRank = 4
)]
public sealed class BaoYueGuangWangGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>五转单点窗口。</summary>
    public override int MaxGuRank => 5;

    /// <summary>五转月系蛊催动需要 2 点元气。</summary>
    public override int YuanQiCost => 2;

    /// <summary>五转月系蛊冷却 3 回合。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(BaoYueWangYi));

    public IReadOnlyList<CompanionDefinition> CompanionDefinitions =>
        [new(typeof(BaoYueWangYi)), new(typeof(YueWangHui))];

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<BaoYueWangYiEnhancement>(
                amount: 1,
                magnitude: 4,
                usesPerCombat: 1,
                companionTypes: [typeof(BaoYueWangYi)]
            ),
            CompanionEnhancementGrant.Of<YueWangHuiEnhancement>(
                amount: 1,
                magnitude: 1,
                usesPerCombat: 1,
                companionTypes: [typeof(YueWangHui)]
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public BaoYueGuangWangGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }
}
