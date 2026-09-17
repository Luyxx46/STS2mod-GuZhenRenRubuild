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
/// 璇光幕（璇光蛊的第二张伴生技能牌）。
///
/// <para>
/// 与璇光刃形成攻技自循环：基础 6 点格挡，折光是纯功能段「抽 1 张牌」，
/// 被【璇照】强化时额外获得 1 聚光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class XuanGuangMu2 : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(XuanGuangXuanGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Functional;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(6m, 0m);

    public XuanGuangMu2()
        : base(1, CardType.Skill, TargetType.Self)
    {
    }

    protected override Task PlayBaseAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.GainBlockAsync(this, cardPlay);

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
        UpgradeBlockVars();
    }
}
