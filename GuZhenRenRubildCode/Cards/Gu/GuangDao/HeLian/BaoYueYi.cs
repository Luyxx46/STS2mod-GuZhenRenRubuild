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
/// 宝月衣（宝月霓裳蛊的伴生技能牌，单张高质量防御）。
///
/// <para>
/// 基础 9 点格挡；折光是数值 + 功能混合段：额外获得 5 点格挡，
/// 并使下一张攻击牌强制折光。被【宝霓】强化后折光段格挡显著更高。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class BaoYueYi : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(BaoYueNiChangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(9m, 5m);

    public BaoYueYi()
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
        await GuangDaoCardPlay.GainBlockAsync(this, cardPlay, RefractionBlock);
        GuangDaoCardPlay.ForceNextRefraction(
            this,
            GuangDaoForceScope.AttackCardOnly,
            passIndex
        );
    }

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
