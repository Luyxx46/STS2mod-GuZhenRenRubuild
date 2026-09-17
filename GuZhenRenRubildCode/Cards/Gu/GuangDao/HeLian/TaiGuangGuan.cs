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
/// 太光贯（太光蛊的伴生攻击牌）。
///
/// <para>
/// 基础 8 点伤害；折光段额外造成 6 点伤害：自然折光共 14 点，
/// 有 1 层聚光时折光段再结算一次共 20 点，被【太照】强化时可到 24 点。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class TaiGuangGuan : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(TaiGuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(8m, 6m);

    public TaiGuangGuan()
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
