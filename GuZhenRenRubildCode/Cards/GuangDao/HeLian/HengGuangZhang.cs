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
/// 恒光障（恒光仙蛊的第二张伴生技能牌），与 <see cref="HengGuangRen"/> 组成仙级连续折光动作组。
///
/// <para>
/// 基础格挡 = 来源恒光仙蛊转数 + 2（六/七/八转 = 8/9/10）；
/// 折光段额外获得 = 转数 - 1（六/七/八转 = 5/6/7）。
/// 被【恒照】强化时同样在重复之后返还 1 聚光并强制下一张折光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class HengGuangZhang : AbstractGuangDaoCompanionCard
{
    private const decimal FallbackBlock = 8m;
    private const decimal FallbackRefractionBlock = 5m;

    public override Type SourceGuType => typeof(HengGuangXianGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(FallbackBlock, FallbackRefractionBlock);

    public HengGuangZhang()
        : base(1, CardType.Skill, TargetType.Self)
    {
    }

    protected override void RefreshSourceRankValues(int sourceGuRank)
    {
        DynamicVars.Block.BaseValue = sourceGuRank + 2m;
        DynamicVars[RefractionBlockName].BaseValue =
            Math.Max(0m, sourceGuRank - 1m);
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
    ) => HengZhaoRefractionReward.ApplyAsync(
        this,
        choiceContext,
        cardPlay,
        passes
    );

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
