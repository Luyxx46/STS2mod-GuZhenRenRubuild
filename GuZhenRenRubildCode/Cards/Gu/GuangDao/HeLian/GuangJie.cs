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
/// 光界（光蛊的第二张伴生技能牌，九转防御侧接续牌）。
///
/// <para>
/// 基础 9 点格挡，折光段额外获得 7 点格挡（自然 16、聚光重复后 23）；
/// 若本次折光因聚光发生了重复，则重复结束后使下一张拥有折光段的牌强制折光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class GuangJie : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(GuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(9m, 7m);

    public GuangJie()
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

    protected override Task OnRefractionCompletedAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passes
    )
    {
        if (passes >= 2)
        {
            GuangDaoSystem.ForceNextRefraction(
                Owner,
                GuangDaoForceScope.AnyGuangDaoCard
            );
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
