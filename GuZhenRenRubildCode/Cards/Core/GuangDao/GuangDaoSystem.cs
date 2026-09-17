using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 光道的统一折光入口。
///
/// <para>
/// 卡牌只能读取这里已经确定的结果：不得自行推导上一张牌类型、不得重复消费聚光、
/// 不得修改真实折光序号。折光的判定真值保存在 <see cref="ZheGuangPower"/>（隐藏玩家 Power）里。
/// </para>
/// </summary>
public static class GuangDaoSystem
{
    // ---------------------------------------------------------------------
    // 折光判定
    // ---------------------------------------------------------------------

    /// <summary>
    /// 读取本次出牌的折光结果；非本次序列首段、或本牌不是判定对象时返回
    /// <see cref="RefractionResult.None"/>。
    /// </summary>
    public static RefractionResult GetRefractionResult(
        CardModel card,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (!ReferenceEquals(card, cardPlay.Card) || !cardPlay.IsFirstInSeries)
        {
            return RefractionResult.None;
        }

        return GetState(card.Owner?.Creature)?.GetCurrentResult()
            ?? RefractionResult.None;
    }

    /// <summary>
    /// 领取「本次折光段应结算几次」。
    ///
    /// <para>
    /// <b>只有主动调用本入口的折光效果牌才会消费聚光</b>：
    /// 只记录真实折光而没有折光段的牌（例如玉皮甲这类桥接牌）不会空耗聚光。
    /// </para>
    ///
    /// <para>
    /// <paramref name="numeric"/> 为 false 时表示纯功能折光：既不消耗聚光，
    /// 也不会被聚光重复，恒返回 1 次结算。
    /// </para>
    /// </summary>
    public static async Task<RefractionResult> ResolveRefractionEffectAsync(
        PlayerChoiceContext choiceContext,
        CardModel card,
        CardPlay cardPlay,
        bool numeric
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardPlay);

        RefractionResult result = GetRefractionResult(card, cardPlay);
        if (!result.Triggered)
        {
            return result;
        }

        ZheGuangPower? state = GetState(card.Owner?.Creature);
        if (state == null || state.CurrentEffectWasResolved)
        {
            return state?.GetCurrentResult() ?? result;
        }

        state.MarkCurrentEffectResolved();

        if (!numeric)
        {
            return state.GetCurrentResult();
        }

        JuGuangPower? juGuang = GetJuGuangPower(card.Owner?.Creature);
        if (juGuang is not { Amount: > 0 })
        {
            return state.GetCurrentResult();
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            juGuang,
            -1,
            card.Owner!.Creature,
            card
        );

        state.MarkCurrentEffectDoubled();
        return state.GetCurrentResult();
    }

    /// <summary>整场战斗累计的真实折光次数（对外唯一查询 API）。</summary>
    public static int GetTotalRefractionSerial(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return GetState(player.Creature)?.TotalRefractionSerial ?? 0;
    }

    // ---------------------------------------------------------------------
    // 聚光
    // ---------------------------------------------------------------------

    /// <summary>指定玩家当前持有的聚光层数。</summary>
    public static int GetJuGuang(Player? player) =>
        GetJuGuangPower(player?.Creature)?.Amount ?? 0;

    /// <summary>给指定玩家叠加聚光层数（0 或负数时无操作）。</summary>
    public static async Task GrantJuGuangAsync(
        PlayerChoiceContext choiceContext,
        CardModel source,
        int amount
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(source);

        if (amount <= 0 || source.Owner is not { } owner)
        {
            return;
        }

        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            owner.Creature,
            amount,
            owner.Creature,
            source
        );
    }

    /// <summary>消耗 1 层聚光；层数为 0 时返回 false 且不改动状态。</summary>
    public static async Task<bool> TryConsumeJuGuangAsync(
        PlayerChoiceContext choiceContext,
        CardModel source
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(source);

        if (source.Owner is not { } owner ||
            GetJuGuangPower(owner.Creature) is not { Amount: > 0 } juGuang)
        {
            return false;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            juGuang,
            -1,
            owner.Creature,
            source
        );
        return true;
    }

    // ---------------------------------------------------------------------
    // 强制折光
    // ---------------------------------------------------------------------

    /// <summary>令下一张符合范围的带光道标签牌强制折光。</summary>
    public static void ForceNextRefraction(Player? player, GuangDaoForceScope scope)
    {
        if (scope == GuangDaoForceScope.None)
        {
            return;
        }

        GetState(player?.Creature)?.ArmForcedRefraction(scope);
    }

    /// <summary>本回合全部带光道标签的牌无条件折光（九转【光蛊】催动）。</summary>
    public static void ForceRefractionForWholeTurn(Player? player)
    {
        GetState(player?.Creature)?.ArmForcedRefractionForWholeTurn();
    }

    // ---------------------------------------------------------------------
    // 挂载
    // ---------------------------------------------------------------------

    /// <summary>
    /// 战斗开始时静默挂载折光真值 Power（幂等；重连/房间重建会重复调用）。
    /// 折光状态是整场战斗的共享真值，因此必须在任何一张牌被打出之前存在。
    /// </summary>
    internal static async Task EnsureZheGuangAsync(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(player);

        if (player.Creature.CombatState == null ||
            GetState(player.Creature) != null)
        {
            return;
        }

        await PowerCmd.Apply<ZheGuangPower>(
            choiceContext,
            player.Creature,
            1,
            player.Creature,
            cardSource: null,
            silent: true
        );
    }

    private static ZheGuangPower? GetState(Creature? creature) =>
        creature?.GetPower<ZheGuangPower>();

    private static JuGuangPower? GetJuGuangPower(Creature? creature) =>
        creature?.GetPower<JuGuangPower>();
}
