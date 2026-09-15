using GuZhenRenRubild.Cards.Core.Abstractions;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Rules;

/// <summary>篝火升炼的纯规则层。</summary>
public static class GuRankUpRules
{
    public const int SlotBudget = 2;
    public const int AdvancedRankThreshold = 6;

    /// <summary>
    /// 一张蛊牌当前能否被升炼选中。
    ///
    /// 除"未达自身转数上限"外还要过仙蛊唯一性：整局中已有同名仙蛊时，
    /// 这只蛊不能再升到六转，因此在选牌界面里就应当是不可选的（灰显），
    /// 而不是让玩家选中后再静默失败。
    /// </summary>
    public static bool CanRankUp(CardModel card) =>
        card is AbstractGuCard gu &&
        gu.GuRank < gu.MaxGuRank &&
        GuXianGuRules.CanReachGuRank(gu, gu.GuRank + 1);

    public static int GetSlotCost(CardModel card)
    {
        if (card is not AbstractGuCard gu)
        {
            return int.MaxValue;
        }

        return gu.GuRank < AdvancedRankThreshold ? 1 : 2;
    }
}
