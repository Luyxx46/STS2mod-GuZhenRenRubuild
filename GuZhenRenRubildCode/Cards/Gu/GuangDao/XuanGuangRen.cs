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
/// 璇光刃（璇光蛊的伴生攻击牌）。
///
/// <para>
/// 基础 6 点伤害；折光是<b>纯功能</b>段：抽 1 张牌（扇出资源），
/// 被【璇照】强化时该次折光后额外获得 1 聚光（聚光用于后续数值折光而不是自己重复）。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class XuanGuangRen : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(XuanGuangXuanGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Functional;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(6m, 0m);

    public XuanGuangRen()
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

    protected override async Task PlayRefractionSegmentAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passIndex
    )
    {
        await GuangDaoCardPlay.DrawAsync(choiceContext, this, 1m);
        await GuangDaoCardPlay.GainJuGuangAsync(
            choiceContext,
            this,
            baseAmount: 0,
            passIndex
        );
    }

    protected override void OnUpgrade()
    {
        UpgradeAttackVars();
    }
}
