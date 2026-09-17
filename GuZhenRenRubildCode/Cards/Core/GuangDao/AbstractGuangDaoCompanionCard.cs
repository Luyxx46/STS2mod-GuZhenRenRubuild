using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 光道伴生牌（普通动作牌）的公共父类。
///
/// <para>
/// 结构固定为两段：<b>基础动作</b>（伤害 / 格挡，走原生数值与升级）
/// 之后按统一入口结算<b>折光段</b>。
/// 带光道标签的伴生牌才会触发折光；不带标签的普通牌是<b>桥接牌</b>，
/// 照常提交出牌类型，但不触发也不消费强制折光标记
/// （当前模组内暂无桥接牌实例，机制保留备用）。
/// </para>
///
/// <para>
/// 折光段的数值由卡面 <c>RefractionDamage</c> / <c>RefractionBlock</c> 动态变量
/// 与强化附加值共同组成，因此「强化会随折光段一起被聚光重复」是自然结果。
/// </para>
/// </summary>
public abstract class AbstractGuangDaoCompanionCard : AbstractCompanionCard
{
    /// <summary>折光段伤害动态变量名（卡面与结算共用同一来源）。</summary>
    public const string RefractionDamageName = "RefractionDamage";

    /// <summary>折光段格挡动态变量名。</summary>
    public const string RefractionBlockName = "RefractionBlock";

    protected AbstractGuangDaoCompanionCard(
        int energyCost,
        CardType type,
        TargetType target
    ) : base(energyCost, type, CardRarity.Basic, target)
    {
    }

    /// <summary>光道标签 = 自带折光段。</summary>
    protected override HashSet<CardTag> CanonicalTags =>
        new() { GuangDaoTags.GuangDao };

    /// <summary>技能型折光伴生会获得格挡，攻击型不会。</summary>
    public override bool GainsBlock => Type == CardType.Skill;

    /// <summary>本牌折光段的分类；默认数值型，纯功能折光覆写成 Functional。</summary>
    protected virtual GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Numeric;

    protected sealed override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        // 结算前先按来源蛊当前转数刷新一次，确保卡面数值与实际结算一致。
        ApplySourceRankValues();

        await PlayBaseAsync(choiceContext, cardPlay);

        int passes = await GuangDaoCardPlay.ResolveRefractionSegmentAsync(
            this,
            choiceContext,
            cardPlay,
            RefractionKind,
            passIndex => PlayRefractionSegmentAsync(
                choiceContext,
                cardPlay,
                passIndex
            )
        );

        await OnRefractionCompletedAsync(choiceContext, cardPlay, passes);
    }

    /// <summary>
    /// 折光段全部结算完成后的收口钩子。
    /// <paramref name="passes"/> 为实际结算遍数：0 表示未折光、2 表示被聚光重复过。
    /// 需要「重复之后才发生」的收益（恒照、光耀、光界）覆写这里。
    /// </summary>
    protected virtual Task OnRefractionCompletedAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passes
    ) => Task.CompletedTask;

    /// <summary>
    /// 卡面文本构建时同步来源蛊转数派生值。
    ///
    /// <para>
    /// <c>CardModel.GetDescriptionForPile</c> 先把 DynamicVar 对象挂进 LocString、
    /// 之后才调用本方法，因此这里改 <c>BaseValue</c> 仍然影响这一次的显示结果；
    /// 只有可变实例才写（规范实例是全局限共用的模板，绝不能被污染）。
    /// </para>
    /// </summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        ApplySourceRankValues();
    }

    /// <summary>
    /// 派生值刷新钩子：<paramref name="sourceGuRank"/> 为来源蛊当前转数
    /// （0 表示找不到来源蛊，应保持卡面基础值）。需要随来源蛊转数成长的伴生覆写它。
    /// </summary>
    protected virtual void RefreshSourceRankValues(int sourceGuRank)
    {
    }

    /// <summary>按来源蛊转数刷新派生值；找不到来源蛊或非可变实例时不做任何事。</summary>
    private void ApplySourceRankValues()
    {
        int rank = ResolveSourceGuRank();
        if (rank > 0)
        {
            RefreshSourceRankValues(rank);
        }
    }

    /// <summary>
    /// 读取本伴生来源蛊的当前转数：在拥有者的永久牌组里按
    /// <see cref="AbstractCompanionCard.SourceGuType"/> 找同名来源蛊，取最高转数。
    ///
    /// <para>
    /// 直接扫描牌组而不走伴生配对映射：战斗中前者同样可用，
    /// 且不依赖结算顺序与网络编号，读到的永远是这份存档的真实转数。
    /// </para>
    /// </summary>
    private int ResolveSourceGuRank()
    {
        if (!IsMutable || Owner == null)
        {
            return 0;
        }

        int highest = 0;
        foreach (CardModel card in Owner.Deck.Cards)
        {
            if (card is IGuCard gu && card.GetType() == SourceGuType)
            {
                highest = Math.Max(highest, gu.GuRank);
            }
        }

        return highest;
    }

    /// <summary>基础动作（卡面数值部分）。</summary>
    protected virtual Task PlayBaseAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => Task.CompletedTask;

    /// <summary>
    /// 折光段本体；<paramref name="passIndex"/> 为 0 表示正常结算，
    /// 1 表示聚光重复出来的那一遍。整段（含强化附加值）会被重复执行。
    /// </summary>
    protected virtual Task PlayRefractionSegmentAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passIndex
    ) => Task.CompletedTask;

    // ---------------------------------------------------------------------
    // 折光段数值
    // ---------------------------------------------------------------------

    /// <summary>本次折光段的伤害 = 卡面折光值 + 强化附加值。</summary>
    protected decimal RefractionDamage(int passIndex) =>
        DynamicVars[RefractionDamageName].BaseValue +
        (GuangDaoCardPlay.ReadEnhancement(this)
            ?.RefractionDamageBonus(passIndex) ?? 0m);

    /// <summary>本次折光段的格挡 = 卡面折光值 + 强化附加值。</summary>
    protected decimal RefractionBlock =>
        DynamicVars[RefractionBlockName].BaseValue +
        (GuangDaoCardPlay.ReadEnhancement(this)?.RefractionBlockBonus ?? 0m);

    // ---------------------------------------------------------------------
    // 数值声明辅助
    // ---------------------------------------------------------------------

    /// <summary>攻击型折光伴生的标准动态变量：基础伤害 + 折光伤害。</summary>
    protected static IEnumerable<DynamicVar> AttackVars(
        decimal damage,
        decimal refractionDamage
    ) =>
    [
        new DamageVar(damage, ValueProp.Move),
        new DamageVar(RefractionDamageName, refractionDamage, ValueProp.Move),
    ];

    /// <summary>技能型折光伴生的标准动态变量：基础格挡 + 折光格挡。</summary>
    protected static IEnumerable<DynamicVar> BlockVars(
        decimal block,
        decimal refractionBlock
    ) =>
    [
        new BlockVar(block, ValueProp.Move),
        new BlockVar(RefractionBlockName, refractionBlock, ValueProp.Move),
    ];

    /// <summary>攻击型折光伴生的标准升级：基础 +3 伤害、折光 +1 伤害。</summary>
    protected void UpgradeAttackVars()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        if (DynamicVars.ContainsKey(RefractionDamageName))
        {
            DynamicVars[RefractionDamageName].UpgradeValueBy(1m);
        }
    }

    /// <summary>技能型折光伴生的标准升级：基础 +3 格挡、折光 +1 格挡。</summary>
    protected void UpgradeBlockVars()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        if (DynamicVars.ContainsKey(RefractionBlockName))
        {
            DynamicVars[RefractionBlockName].UpgradeValueBy(1m);
        }
    }
}
