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

/// <summary>月霓（月霓裳蛊的伴生技能牌）：基础 5 点格挡，折光段额外获得 3 点格挡。</summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YueNi : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(YueNiChangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(5m, 3m);

    public YueNi()
        : base(1, CardType.Skill, TargetType.Self)
    {
    }

    protected override Task PlayBaseAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.GainBlockAsync(this, cardPlay);

    protected override Task PlayRefractionSegmentAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passIndex
    ) => GuangDaoCardPlay.GainBlockAsync(this, cardPlay, RefractionBlock);

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
