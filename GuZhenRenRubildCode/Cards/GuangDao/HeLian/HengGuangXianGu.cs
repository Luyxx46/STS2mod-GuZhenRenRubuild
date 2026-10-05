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
/// 恒光仙蛊（六转—八转 · 六转并流主线）。
///
/// <para>
/// 合练：太光蛊 + 宝月光王蛊（两张材料均需至少 5 转），结果转数 = 材料最高转数 + 1 = 6；
/// 之后由篝火升炼推进到七转、八转。
/// </para>
///
/// <para>
/// B 形态纯增幅：两张伴生各获得按转数递增次数的【恒照】
/// （六转 1 次、七转 2 次、八转 3 次）：若本次折光因聚光发生重复，
/// 则重复结算后获得 1 聚光，并使下一张拥有折光段的牌强制折光。
/// 返还只绑定恒光伴生且有次数上限，因此不会让全牌组永久免费重复。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(TaiGuangGu),
    typeof(BaoYueGuangWangGu),
    MinimumMaterialRank = 5
)]
public sealed class HengGuangXianGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：六至八转。</summary>
    public override int MaxGuRank => 8;

    /// <summary>仙蛊催动需要 2 点元气。</summary>
    public override int YuanQiCost => 2;

    /// <summary>仙蛊冷却 3 回合。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(HengGuangRen));

    public IReadOnlyList<CompanionDefinition> CompanionDefinitions =>
        [new(typeof(HengGuangRen)), new(typeof(HengGuangZhang))];

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<HengZhaoEnhancement>(
                amount: 1,
                magnitude: 1,
                usesPerCombat: Math.Clamp(GuRank - 5, 1, 3)
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    public HengGuangXianGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }
}
