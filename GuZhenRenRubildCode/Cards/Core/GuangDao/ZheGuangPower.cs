using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 隐藏的玩家级折光战斗状态。
///
/// <para>
/// 所有会影响结算的字段都保存在本 Power 的 <see cref="PowerModel.DynamicVars"/> 中，
/// 因此读档、克隆副本与多人快照天然共用同一份战斗真值，不需要额外持久化层。
/// 对玩家不可见（<see cref="IsVisibleInternal"/> 恒 false）。
/// </para>
///
/// <para>
/// 判定对象只包含<b>普通牌</b>（标准类型，且不是蛊牌）：
/// 蛊牌走蛊手牌层，不参与类型交替链。
/// 自动出牌（<see cref="CardPlay.IsAutoPlay"/>）完全绕过。
/// </para>
/// </summary>
[RegisterPower]
public sealed class ZheGuangPower : ModPowerTemplate
{
    // 上一张主动打出的普通牌类型（CardType.None 表示本回合还没有）。
    private const string PreviousTypeKey = "PreviousCardType";

    // 整场战斗累计的真实折光次数（对外唯一查询 API，供未来的卡牌级资源使用）。
    private const string TotalSerialKey = "TotalRefractionSerial";

    // 强制折光标记：0 = 无，1 = 下一张光道牌，2 = 下一张光道攻击牌。
    private const string ForceNextKey = "ForceNextGuangDaoRefraction";

    // 本回合全部光道牌无条件折光（九转【光蛊】催动）。
    private const string ForceAllTurnKey = "ForceAllGuangDaoRefractionThisTurn";

    // 本次出牌的判定结果与效果结算次数。
    private const string CurrentTriggeredKey = "CurrentTriggered";
    private const string CurrentEffectCountKey = "CurrentEffectCount";
    private const string CurrentEffectResolvedKey = "CurrentEffectResolved";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>折光是内部真值，不显示在状态栏上。</summary>
    protected override bool IsVisibleInternal => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(PreviousTypeKey, (int)CardType.None),
        new DynamicVar(TotalSerialKey, 0),
        new DynamicVar(ForceNextKey, 0),
        new DynamicVar(ForceAllTurnKey, 0),
        new DynamicVar(CurrentTriggeredKey, 0),
        new DynamicVar(CurrentEffectCountKey, 0),
        new DynamicVar(CurrentEffectResolvedKey, 0),
    ];

    /// <summary>本回合上一张主动打出的普通牌类型。</summary>
    public CardType PreviousCardType =>
        (CardType)DynamicVars[PreviousTypeKey].IntValue;

    /// <summary>整场战斗累计的真实折光次数。</summary>
    public int TotalRefractionSerial =>
        Math.Max(0, DynamicVars[TotalSerialKey].IntValue);

    /// <summary>本次折光段是否已经被统一入口领取过（防止重复消费聚光）。</summary>
    internal bool CurrentEffectWasResolved =>
        DynamicVars[CurrentEffectResolvedKey].IntValue != 0;

    /// <summary>回合开始清空「上一张类型」与本次判定，<b>不清</b>强制折光标记与累计序号。</summary>
    public override Task AfterEnergyReset(Player player)
    {
        if (ReferenceEquals(player, Owner.Player))
        {
            DynamicVars[PreviousTypeKey].BaseValue = (int)CardType.None;
            DynamicVars[ForceAllTurnKey].BaseValue = 0;
            ClearCurrentResult();
        }

        return Task.CompletedTask;
    }

    /// <summary>出牌前判定折光：只有本牌属于判定对象时才刷新结果。</summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.IsAutoPlay ||
            !ReferenceEquals(cardPlay.Player.Creature, Owner))
        {
            return Task.CompletedTask;
        }

        CardModel card = cardPlay.Card;

        // 蛊牌与诅咒/状态等特殊类型完全不参与：它们既不触发折光，
        // 也不改动「上一张类型」，避免蛊手牌层的动作打断普通牌交替链。
        if (card is Abstractions.IGuCard || !IsStandardType(card.Type))
        {
            return Task.CompletedTask;
        }

        ClearCurrentResult();
        if (!cardPlay.IsFirstInSeries)
        {
            return Task.CompletedTask;
        }

        // 桥接牌（无光道标签的普通牌）只提交历史，不触发折光、不消费标记。
        if (!card.IsGuangDaoCard())
        {
            return Task.CompletedTask;
        }

        bool forceAll = DynamicVars[ForceAllTurnKey].IntValue != 0;
        int forceScope = DynamicVars[ForceNextKey].IntValue;
        bool forced = forceAll || IsForceScopeSatisfied(
            (GuangDaoForceScope)forceScope,
            card.Type
        );

        // 自然折光与强制折光同时成立时只产生一个真实折光事件。
        if (forced)
        {
            DynamicVars[ForceNextKey].BaseValue = 0;
        }

        bool natural =
            PreviousCardType != CardType.None &&
            PreviousCardType != card.Type;

        if (!natural && !forced)
        {
            return Task.CompletedTask;
        }

        DynamicVars[CurrentTriggeredKey].BaseValue = 1;
        DynamicVars[CurrentEffectCountKey].BaseValue = 1;
        DynamicVars[TotalSerialKey].BaseValue = TotalRefractionSerial + 1;
        return Task.CompletedTask;
    }

    /// <summary>整段复播/连锁全部完成后才把本牌类型提交进历史。</summary>
    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsAutoPlay &&
            ReferenceEquals(cardPlay.Player.Creature, Owner) &&
            cardPlay.IsLastInSeries &&
            cardPlay.Card is not Abstractions.IGuCard &&
            IsStandardType(cardPlay.Card.Type))
        {
            DynamicVars[PreviousTypeKey].BaseValue =
                (int)cardPlay.Card.Type;
            ClearCurrentResult();
        }

        return Task.CompletedTask;
    }

    internal RefractionResult GetCurrentResult() => new(
        DynamicVars[CurrentTriggeredKey].IntValue != 0,
        Math.Max(0, DynamicVars[CurrentEffectCountKey].IntValue)
    );

    internal void MarkCurrentEffectResolved()
    {
        DynamicVars[CurrentEffectResolvedKey].BaseValue = 1;
    }

    internal void MarkCurrentEffectDoubled()
    {
        DynamicVars[CurrentEffectCountKey].BaseValue = 2;
    }

    internal void ArmForcedRefraction(GuangDaoForceScope scope)
    {
        if (scope == GuangDaoForceScope.None)
        {
            return;
        }

        DynamicVars[ForceNextKey].BaseValue = (int)scope;
    }

    internal void ArmForcedRefractionForWholeTurn()
    {
        DynamicVars[ForceAllTurnKey].BaseValue = 1;
    }

    private static bool IsForceScopeSatisfied(
        GuangDaoForceScope scope,
        CardType cardType
    ) => scope switch
    {
        GuangDaoForceScope.AnyGuangDaoCard => true,
        GuangDaoForceScope.AttackCardOnly => cardType == CardType.Attack,
        _ => false,
    };

    private void ClearCurrentResult()
    {
        DynamicVars[CurrentTriggeredKey].BaseValue = 0;
        DynamicVars[CurrentEffectCountKey].BaseValue = 0;
        DynamicVars[CurrentEffectResolvedKey].BaseValue = 0;
    }

    private static bool IsStandardType(CardType type) => type is
        CardType.Attack or CardType.Skill or CardType.Power;
}
