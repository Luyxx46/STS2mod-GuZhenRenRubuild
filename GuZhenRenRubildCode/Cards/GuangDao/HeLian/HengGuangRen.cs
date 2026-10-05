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
/// 恒光刃（恒光仙蛊的伴生攻击牌）。
///
/// <para>
/// 基础伤害 = 来源恒光仙蛊转数 + 2（六/七/八转 = 8/9/10）；
/// 折光段额外造成 = 转数 - 1（六/七/八转 = 5/6/7）。
/// </para>
///
/// <para>
/// 被【恒照】强化时：若本次折光因聚光发生重复，则重复结算后获得 1 聚光，
/// 并使下一张拥有折光段的牌强制折光——把已经发生的重复继续传向下一张动作。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class HengGuangRen : AbstractGuangDaoCompanionCard
{
    // 找不到来源蛊时的兜底（六转基准值）。
    private const decimal FallbackDamage = 8m;
    private const decimal FallbackRefractionDamage = 5m;

    public override Type SourceGuType => typeof(HengGuangXianGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        AttackVars(FallbackDamage, FallbackRefractionDamage);

    public HengGuangRen()
        : base(1, CardType.Attack, TargetType.AnyEnemy)
    {
    }

    /// <summary>按来源蛊转数刷新基础伤害与折光伤害。</summary>
    protected override void RefreshSourceRankValues(int sourceGuRank)
    {
        DynamicVars.Damage.BaseValue = sourceGuRank + 2m;
        DynamicVars[RefractionDamageName].BaseValue = Math.Max(0m, sourceGuRank - 1m);
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
        UpgradeAttackVars();
    }
}
