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
/// 月王辉（宝月光王蛊的第二张伴生技能牌）。
///
/// <para>
/// 基础 5 点格挡；折光是<b>纯功能</b>段：使下一张攻击牌强制折光，并获得 1 聚光。
/// 被【月王辉强化】时该次共获得 2 聚光。
/// </para>
///
/// <para>
/// 它负责给下一次攻击准备「强制折光 + 重复机会」，本身不消费聚光也不会被重复。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YueWangHui : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(BaoYueGuangWangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Functional;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(5m, 0m);

    public YueWangHui()
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
        GuangDaoCardPlay.ForceNextRefraction(
            this,
            GuangDaoForceScope.AttackCardOnly,
            passIndex
        );

        await GuangDaoCardPlay.GainJuGuangAsync(
            choiceContext,
            this,
            baseAmount: 1,
            passIndex
        );
    }

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
