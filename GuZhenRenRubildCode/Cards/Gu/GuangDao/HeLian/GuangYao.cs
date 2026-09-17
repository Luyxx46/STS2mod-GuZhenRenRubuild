using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.HeLian;

/// <summary>
/// 光耀（光蛊的伴生攻击牌）。
///
/// <para>
/// 基础 9 点伤害，折光段额外造成 7 点伤害（自然 16、聚光重复后 23）；
/// 若本次折光<b>因聚光发生了重复</b>，则重复结束后获得 1 聚光，
/// 使九转可以维持高强度链而不至于无限暴涨。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class GuangYao : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(GuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(9m, 7m);

    public GuangYao()
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

    protected override Task OnRefractionCompletedAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passes
    ) => passes < 2
        ? Task.CompletedTask
        : GuangDaoSystem.GrantJuGuangAsync(choiceContext, this, 1);

    protected override void OnUpgrade()
    {
        UpgradeAttackVars();
    }
}
