using GuZhenRenRubild.Cards.Core;
using GuZhenRenRubild.Cards.Gu;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>月光蛊的验证用永久伴生普通牌。</summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YueGuangCompanion : AbstractCompanionCard
{
    public override Type SourceGuType => typeof(YueGuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildStrike.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move)];

    public YueGuangCompanion()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.AttackAsync(
        this,
        choiceContext,
        cardPlay,
        GuCardPlay.DefaultAttackHitFx
    );

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
