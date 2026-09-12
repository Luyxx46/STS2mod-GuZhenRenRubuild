using GuZhenRenRubild.Cards.Core;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Gu;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.HeLian;

// 玄铁蛊：由玉皮蛊配铁甲蛊合练而成，是防御向的合练结果。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(typeof(YuPiGu), typeof(TieJiaGu))]
public sealed class XuanTieGu : AbstractHeLianGuCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(14m, ValueProp.Move)];

    public override bool GainsBlock => true;

    public override int RecoveryDelayTurns => 3;

    // 暂时复用模板防御牌卡图，后续可替换为玄铁蛊专属资源。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildDefend.png"
    );

    public XuanTieGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        RefreshValues();
    }

    // 合练蛊不能继承防御型蛊牌父类，这里直接复用公共出牌动作。
    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.GainBlockAsync(this, cardPlay);

    protected override void OnGuRankChanged()
    {
        RefreshValues();
    }

    private void RefreshValues()
    {
        // 格挡成长公式：基础 12 点，每提升 1 品阶额外增加 3 点格挡。
        DynamicVars.Block.BaseValue = 12m + GuRank * 3m;
    }
}
