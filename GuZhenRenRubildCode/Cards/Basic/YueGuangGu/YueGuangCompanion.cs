using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Basic.YueGuangGu;

/// <summary>
/// 月光蛊的永久伴生普通牌「月刃」。
///
/// <para>
/// 它是光道动作牌：基础 6 点伤害，之后按统一入口结算折光段「额外造成 3 点伤害」。
/// 父蛊月光蛊催动时挂上的【月华】强化会把折光伤害按层数抬高，
/// 而折光段被聚光重复时这份强化也会跟着再结算一次。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YueGuangCompanion : AbstractGuangDaoCompanionCard
{
    // 目录名与来源蛊类名同为 YueGuangGu，命名空间因此与类型同名，必须用 global:: 全限定名引用。
    public override Type SourceGuType =>
        typeof(global::GuZhenRenRubild.Cards.Basic.YueGuangGu.YueGuangGu);

    // 专属卡图按类名解析：images/cards/YueGuangCompanion.png 存在就用，缺失时回退模板打击图。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(6m, 3m);

    public YueGuangCompanion()
        : base(1, CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override Task PlayBaseAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.AttackAsync(
        this,
        choiceContext,
        cardPlay,
        GuCardPlay.DefaultAttackHitFx
    );

    protected override Task PlayRefractionSegmentAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passIndex
    ) => GuangDaoCardPlay.AttackAsync(
        this,
        choiceContext,
        cardPlay,
        RefractionDamage(passIndex),
        GuCardPlay.DefaultAttackHitFx
    );

    protected override void OnUpgrade()
    {
        UpgradeAttackVars();
    }
}
