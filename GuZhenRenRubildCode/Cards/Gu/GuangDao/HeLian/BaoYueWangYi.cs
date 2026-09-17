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
/// 宝月王衣（宝月光王蛊的伴生技能牌，五转数值防御核心）。
/// 基础 10 点格挡，折光段额外获得 6 点格挡（自然 16、聚光重复后 22）。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class BaoYueWangYi : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(BaoYueGuangWangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(10m, 6m);

    public BaoYueWangYi()
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
