using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Rules;

public static class GuCardRewardRules
{
    public static bool CanAppear(Player player, CardModel candidate)
    {
        if (player.Character is not GuYueFangYuan)
        {
            return true;
        }

        // 杀招既不是蛊牌也不是普通牌，是推演产物。
        // 永远不进入普通卡牌奖励（点名拦一次，避免日后改动这个判断时把它们放进来）。
        if (candidate is AbstractShaZhaoCard)
        {
            return false;
        }

        // 仙元牌同样点名拦下：它们是六转起按空窍转数在战斗内生成的货币牌。
        if (candidate is AbstractXianYuanCard)
        {
            return false;
        }

        if (candidate is not IGuCard)
        {
            return false;
        }

        // 专属合练蛊默认不进普通卡牌奖励：它们是合练的产物，
        // 只有显式实现 IHeLianCardRewardEligible 的才放行。
        return candidate is not AbstractHeLianGuCard ||
            candidate is IHeLianCardRewardEligible;
    }
}
