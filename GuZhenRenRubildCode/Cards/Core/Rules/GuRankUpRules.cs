using GuZhenRenRubild.Cards.Core;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Rules;

/// <summary>篝火升炼的纯规则层。</summary>
public static class GuRankUpRules
{
    public const int SlotBudget = 2;
    public const int AdvancedRankThreshold = 6;

    public static bool CanRankUp(CardModel card) =>
        card is AbstractGuCard gu && gu.GuRank < gu.MaxGuRank;

    public static int GetSlotCost(CardModel card)
    {
        if (card is not AbstractGuCard gu)
        {
            return int.MaxValue;
        }

        return gu.GuRank < AdvancedRankThreshold ? 1 : 2;
    }
}
