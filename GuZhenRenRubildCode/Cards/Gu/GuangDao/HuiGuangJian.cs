using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao;

/// <summary>
/// 辉光矢（辉光蛊的伴生攻击牌）。
/// 基础 5 点伤害，折光段额外造成 3 点伤害——低阶最稳定、最值得被聚光重复的折光动作。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class HuiGuangJian : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(HuiGuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(5m, 3m);

    public HuiGuangJian()
        : base(1, CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override Task PlayBaseAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.AttackAsync(
        this,
        choiceContext,
        cardPlay,
        GuCardPlay.DefaultAttackHitFx
    );

    protected override Task PlayRefractionSegmentAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passIndex
    ) => GuangDaoCardPlay.AttackAsync(
        this,
        choiceContext,
        cardPlay,
        RefractionDamage(passIndex),
        GuCardPlay.DefaultAttackHitFx
    );

    protected override void OnUpgrade()
    {
        UpgradeAttackVars();
    }
}
