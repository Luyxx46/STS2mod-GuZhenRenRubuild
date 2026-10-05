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
/// 引光（光源蛊的伴生技能牌）。
///
/// <para>
/// 基础 4 点格挡；折光是<b>纯功能</b>段：回复 1 点元气。
/// 它既不消耗聚光、也不会被聚光重复，是蛊牌冷却空窗期的续航动作。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YinGuang : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(GuangYuanGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    // 折光段没有伤害/格挡数值（只有「回复 1 元气」），因此归类为纯功能折光。
    protected override GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Functional;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(4m, 0m);

    public YinGuang()
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
        // 基础功能段：回复 1 元气。
        await GuangDaoCardPlay.GainYuanQiAsync(this, 1);

        // 父蛊催动给出的额外格挡是「强化附加值」：只在这一次折光里补一次，
        // 不会把引光变成数值型折光，因此不会消耗聚光也不会被重复。
        await GuangDaoCardPlay.GainBlockAsync(
            this,
            cardPlay,
            GuangDaoCardPlay.ReadEnhancement(this)?.RefractionBlockBonus ?? 0m
        );
    }

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
