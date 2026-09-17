using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.HeLian;

/// <summary>
/// 光蛊（九转 · 六转并流终点）。
///
/// <para>
/// 合练：恒光仙蛊 + 太光蛊（两张材料均需至少 5 转）；这是九转终点，
/// 因此结果转数固定为九转，而不随材料转数浮动。
/// </para>
///
/// <para>
/// 规则级增幅：催动使<b>本回合所有拥有折光段的普通牌均视为满足折光条件</b>
/// （<see cref="GuangDaoSystem.ForceRefractionForWholeTurn"/>）。
/// 聚光仍按正常规则逐次消耗，不会变成无限资源；九转的优势是让玩家
/// 把有限聚光精准投到想重复的数值牌上。
/// </para>
///
/// <para>
/// 两张伴生不带额外强化声明：光耀与光界各自在重复之后自带返还 / 接续条款。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(HengGuangXianGu),
    typeof(TaiGuangGu),
    MinimumMaterialRank = 5
)]
public sealed class GuangGu : AbstractGuangDaoHeLianGuCard
{
    /// <summary>九转单点窗口。</summary>
    public override int MaxGuRank => 9;

    /// <summary>九转规则级蛊牌催动需要 3 点元气。</summary>
    public override int YuanQiCost => 3;

    /// <summary>九转规则级蛊牌冷却 4 回合。</summary>
    public override int RecoveryDelayTurns => 4;

    public CompanionDefinition Companion => new(typeof(GuangYao));

    public IReadOnlyList<CompanionDefinition> CompanionDefinitions =>
        [new(typeof(GuangYao)), new(typeof(GuangJie))];

    /// <summary>九转终点：结果转数固定为九转。</summary>
    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    ) => MaxGuRank;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    public GuangGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        // 本回合折光条件自动满足；聚光仍按正常规则逐次消耗。
        GuangDaoSystem.ForceRefractionForWholeTurn(Owner);
        return Task.CompletedTask;
    }
}
