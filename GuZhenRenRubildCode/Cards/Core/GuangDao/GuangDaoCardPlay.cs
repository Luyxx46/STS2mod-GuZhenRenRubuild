using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 卡面折光段的统一执行入口。
///
/// <para>
/// 卡牌只描述「折光段做什么」，是否触发、结算几次由 <see cref="GuangDaoSystem"/> 决定。
/// 折光段按 <c>passIndex</c> 逐遍执行：0 是正常结算，1 是聚光重复出来的那一遍，
/// 强化可以用它表达「只在重复那遍加伤」这类效果。
/// </para>
/// </summary>
public static class GuangDaoCardPlay
{
    /// <summary>
    /// 结算本牌的折光段。
    /// </summary>
    /// <param name="card">正在结算的卡牌。</param>
    /// <param name="choiceContext">当前出牌上下文。</param>
    /// <param name="cardPlay">本次出牌。</param>
    /// <param name="kind">折光段分类；<see cref="GuangDaoRefractionKind.None"/> 直接跳过。</param>
    /// <param name="segment">
    /// 折光段本体；<c>passIndex</c> 从 0 开始。整段（含强化附加值）会在重复时再跑一遍。
    /// </param>
    /// <returns>折光段实际结算的遍数（0 = 未触发折光）。</returns>
    public static async Task<int> ResolveRefractionSegmentAsync(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        GuangDaoRefractionKind kind,
        Func<int, Task> segment
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay);
        ArgumentNullException.ThrowIfNull(segment);

        if (kind == GuangDaoRefractionKind.None)
        {
            return 0;
        }

        RefractionResult result =
            await GuangDaoSystem.ResolveRefractionEffectAsync(
                choiceContext,
                card,
                cardPlay,
                numeric: kind == GuangDaoRefractionKind.Numeric
            );

        if (!result.Triggered)
        {
            return 0;
        }

        // 只有真正触发折光时才通知强化：带「折光才生效」条件的强化据此判定
        // 本次是否消耗一次可用次数，未触发的出牌不会白扣。
        ReadSlot(card)?.MarkRefractionTriggered();

        int passes = Math.Max(1, result.EffectResolutionCount);
        for (int passIndex = 0; passIndex < passes; passIndex++)
        {
            await segment(passIndex);
        }

        return passes;
    }

    /// <summary>读取本牌强化槽里的光道强化附加值；没有强化或非光道强化时返回 null。</summary>
    public static IGuangDaoCompanionEnhancement? ReadEnhancement(CardModel? card) =>
        ReadSlot(card) as IGuangDaoCompanionEnhancement;

    /// <summary>读取本牌强化槽内容本体（用于登记「本次折光已触发」）。</summary>
    internal static AbstractCompanionEnhancement? ReadSlot(CardModel? card) =>
        CompanionEnhancementService.TryGetEnhancement(card);

    // ---------------------------------------------------------------------
    // 折光段常用动作
    // ---------------------------------------------------------------------

    /// <summary>折光段的额外伤害。数值非正时不结算。</summary>
    public static async Task AttackAsync(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        decimal damage,
        string hitFxPath
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        if (damage <= 0m ||
            cardPlay.Target == null ||
            !card.IsValidTarget(cardPlay.Target))
        {
            return;
        }

        await DamageCmd
            .Attack(damage)
            .FromCard(card, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(hitFxPath)
            .Execute(choiceContext);
    }

    /// <summary>折光段的额外格挡。走裸值入口，避免与卡面基础格挡的附魔加成互相叠加。</summary>
    public static Task GainBlockAsync(
        CardModel card,
        CardPlay cardPlay,
        decimal amount
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        return amount <= 0m
            ? Task.CompletedTask
            : CreatureCmd.GainBlock(
                card.Owner.Creature,
                amount,
                ValueProp.Move,
                cardPlay
            );
    }

    /// <summary>折光段的抽牌。</summary>
    public static Task DrawAsync(
        PlayerChoiceContext choiceContext,
        CardModel card,
        decimal count
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        return count <= 0m
            ? Task.CompletedTask
            : CardPileCmd.Draw(choiceContext, count, card.Owner);
    }

    /// <summary>折光段的元气回复。</summary>
    public static Task GainYuanQiAsync(CardModel card, int amount)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (amount <= 0 || card.Owner is not { } owner)
        {
            return Task.CompletedTask;
        }

        int current = SecondaryResourceCmd.Get(owner, YuanQiSystem.ResourceId);
        int maximum =
            SecondaryResourceCmd.GetMax(owner, YuanQiSystem.ResourceId)
            ?? YuanQiSystem.Definition.HardMaxAmount;

        int target = Math.Clamp(
            current + amount,
            YuanQiSystem.Definition.MinAmount,
            maximum
        );

        return target == current
            ? Task.CompletedTask
            : SecondaryResourceCmd.Set(
                owner,
                YuanQiSystem.ResourceId,
                target,
                source: card
            );
    }

    /// <summary>折光段的聚光获取（基础量 + 强化附加值）。</summary>
    public static Task GainJuGuangAsync(
        PlayerChoiceContext choiceContext,
        CardModel card,
        int baseAmount,
        int passIndex
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        int bonus = passIndex == 0
            ? ReadEnhancement(card)?.ExtraJuGuangOnRefraction ?? 0
            : 0;

        return GuangDaoSystem.GrantJuGuangAsync(
            choiceContext,
            card,
            baseAmount + bonus
        );
    }

    /// <summary>折光段的功能部分：令下一张符合范围的光道牌强制折光。</summary>
    public static void ForceNextRefraction(
        CardModel card,
        GuangDaoForceScope scope,
        int passIndex
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        // 强制折光是布尔状态，多次赋予不叠加；重复那遍只用来兑现「附加数值」。
        if (passIndex > 0)
        {
            return;
        }

        bool fromEnhancement =
            ReadEnhancement(card)?.ForcesNextRefraction == true;

        GuangDaoSystem.ForceNextRefraction(
            card.Owner,
            fromEnhancement ? GuangDaoForceScope.AnyGuangDaoCard : scope
        );
    }

    /// <summary>把一张牌本回合的费用按强化声明下调（回照类效果）。</summary>
    public static void ApplyRecoverDiscount(CardModel card, CardModel enhancementHost)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(enhancementHost);

        if (card.EnergyCost.CostsX ||
            ReadEnhancement(enhancementHost) is not { } enhancement)
        {
            return;
        }

        int reduction = enhancement.RecoverCostReduction;
        if (reduction < 0)
        {
            return;
        }

        int baseCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);

        card.EnergyCost.SetThisTurnOrUntilPlayed(
            reduction == 0 ? 0 : Math.Max(0, baseCost - reduction)
        );
    }

    /// <summary>玩家当前聚光层数。</summary>
    public static int GetJuGuang(Player? player) => GuangDaoSystem.GetJuGuang(player);
}
