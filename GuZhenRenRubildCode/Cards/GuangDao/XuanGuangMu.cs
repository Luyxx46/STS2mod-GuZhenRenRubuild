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
/// 炫光幕（炫光蛊的伴生技能牌）。
///
/// <para>
/// 基础 5 点格挡；折光是<b>数值 + 功能混合</b>段：额外获得 2 点格挡，
/// 并使下一张光道攻击牌强制折光。聚光重复的是整段折光，但强制折光属于布尔状态，
/// 多次赋予不会叠加。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class XuanGuangMu : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(XuanGuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(5m, 2m);

    public XuanGuangMu()
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
