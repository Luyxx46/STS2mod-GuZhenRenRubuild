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
/// 皓月引（皓月霓裳蛊的第二张伴生技能牌）。
///
/// <para>
/// 基础 4 点格挡；折光是<b>数值 + 功能混合</b>段：额外获得 2 点格挡，
/// 并使下一张攻击牌强制折光——负责为攻击侧的折光做接续。
/// 聚光重复整段折光，但强制折光不叠加。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class HaoYueYin : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(HaoYueNiChangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(4m, 2m);

    public HaoYueYin()
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
