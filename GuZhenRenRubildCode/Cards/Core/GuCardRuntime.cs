using GuZhenRenRubild.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards;

/// <summary>
/// 保存每张蛊牌在当前战斗中的使用次数与恢复回合，并提供统一的激活资格判断。
/// 额外手牌界面会高频调用可用性判定，因此这些方法只做直接状态读取和数值比较，避免产生额外临时对象。
/// </summary>
public static class GuCardRuntime
{
    // 记录当前激活周期已经消耗的使用次数；战斗开始和恢复完成时都会清零。
    private static readonly SavedAttachedState<CardModel, int> SpentUsesState = new(
        Entry.ModId + ".gu_spent_uses",
        static () => 0
    );

    // 记录该蛊牌最早可恢复的回合编号；0 表示当前没有进入恢复倒计时。
    private static readonly SavedAttachedState<CardModel, int> RecoveryTurnState = new(
        Entry.ModId + ".gu_recovery_turn",
        static () => 0
    );

    // 计算剩余使用次数。非蛊牌返回 int 最大值，使通用调用方无需额外分支即可视为“不受次数限制”。
    public static int GetRemainingUses(CardModel card)
    {
        return card is IGuCard gu
            ? Math.Max(0, gu.MaxUses - SpentUsesState[card])
            : int.MaxValue;
    }

    // 判断卡牌是否仍允许继续使用；普通卡始终返回 true。
    public static bool CanUse(CardModel card)
    {
        return card is not IGuCard || GetRemainingUses(card) > 0;
    }

    // 判断蛊牌能否从激活区打出：必须是蛊牌、位于激活区、有剩余次数、已进入战斗，并拥有足够元气。
    public static bool CanActivate(CardModel card)
    {
        if (card is not IGuCard gu ||
            card.Pile?.Type != GuCardPileSystem.ActivePileType ||
            GetRemainingUses(card) <= 0 ||
            card.Owner.PlayerCombatState == null)
        {
            return false;
        }

        return SecondaryResourceCmd.Get(card.Owner, YuanQiSystem.ResourceId) >=
            Math.Max(0, gu.YuanQiCost);
    }

    // 在一次蛊牌打出流程开始时登记使用次数，并把计数限制在至少 1、至多 MaxUses 的范围内。
    public static void RegisterActivation(CardModel card)
    {
        if (card is not IGuCard gu)
        {
            return;
        }

        SpentUsesState[card] = Math.Min(
            Math.Max(1, gu.MaxUses),
            SpentUsesState[card] + 1
        );
    }

    // 新战斗开始时清除上一场战斗留下的使用次数和恢复回合。
    public static void ResetForCombat(CardModel card)
    {
        if (card is not IGuCard)
        {
            return;
        }

        SpentUsesState[card] = 0;
        RecoveryTurnState[card] = 0;
    }

    // 根据剩余次数决定打出后的去向。仍可使用则回到激活区；次数耗尽则计算恢复回合并进入恢复区。
    public static PileType GetResultPile(CardModel card)
    {
        if (card is not IGuCard gu || GetRemainingUses(card) > 0)
        {
            return GuCardPileSystem.ActivePileType;
        }

        // 只在第一次进入恢复区时计算恢复终点，避免后续查询重复延长恢复时间。
        if (RecoveryTurnState[card] <= 0)
        {
            int currentTurn = card.Owner.PlayerCombatState?.TurnNumber ?? 1;
            RecoveryTurnState[card] =
                Math.Max(1, currentTurn) + Math.Max(1, gu.RecoveryDelayTurns);
        }

        return GuCardPileSystem.RecoveryPileType;
    }

    // 当已经设置恢复回合且当前回合达到或超过该值时，蛊牌可以结束恢复。
    public static bool IsRecoveryReady(CardModel card, int turnNumber)
    {
        int readyTurn = RecoveryTurnState[card];
        return card is IGuCard && readyTurn > 0 && readyTurn <= turnNumber;
    }

    // 恢复完成后同时清零次数与倒计时，使蛊牌重新进入完整可用状态。
    public static void CompleteRecovery(CardModel card)
    {
        SpentUsesState[card] = 0;
        RecoveryTurnState[card] = 0;
    }
}
