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
/// 借光束（借光蛊的伴生攻击牌）。
///
/// <para>
/// 基础 6 点伤害；折光是<b>纯功能</b>段：若当前聚光为 0 则获得 1 聚光。
/// 被【借辉】强化后，本场前两次折光改为无论是否已有聚光都获得 1 聚光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class JieGuangShu : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(JieGuangGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Functional;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(6m, 0m);

    public JieGuangShu()
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
    )
    {
        bool strengthened =
            (GuangDaoCardPlay.ReadEnhancement(this)
                ?.ExtraJuGuangOnRefraction ?? 0) > 0;

        // 未强化：只在聚光断档时补 1 层。强化后：无条件补 1 层（不是补 2 层）。
        if (!strengthened &&
            GuangDaoCardPlay.GetJuGuang(Owner) > 0)
        {
            return Task.CompletedTask;
        }

        return GuangDaoSystem.GrantJuGuangAsync(choiceContext, this, 1);
    }

    protected override void OnUpgrade()
    {
        UpgradeAttackVars();
    }
}
