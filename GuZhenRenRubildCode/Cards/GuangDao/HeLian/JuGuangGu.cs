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
/// 聚光蛊（四转 · 光道主线，聚光核心）。
///
/// <para>
/// 合练：光虹蛊 + 光源蛊（两张材料均需至少 3 转），结果转数 = 4。
/// </para>
///
/// <para>
/// B 形态纯增幅：把身份从「给下一次折光加数字」改成
/// <b>主动给后续数值牌准备重复结算次数</b>——催动给 <see cref="JuGuangJing"/>
/// 挂 2 次【汇光】，使它每次成功折光共获得 2 聚光。
/// 聚光镜自身的折光是纯功能段，因此不消耗已有聚光、也不会被重复。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(GuangHongGu),
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.GuangYuanGu),
    MinimumMaterialRank = 3
)]
public sealed class JuGuangGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>四转单点窗口。</summary>
    public override int MaxGuRank => 4;

    /// <summary>核心聚光蛊催动需要 2 点元气。</summary>
    public override int YuanQiCost => 2;

    /// <summary>核心聚光蛊冷却 3 回合。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(JuGuangJing));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<HuiGuangEnhancement>(
                amount: 1,
                magnitude: 1,
                usesPerCombat: 2
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public JuGuangGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }
}
