using GuZhenRenRubild.Cards.Core;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Rules;

public static class GuCardRewardRules
{
    public static bool CanAppear(Player player, CardModel candidate)
    {
        if (player.Character is not GuZhenRenRubildCharacter)
        {
            return true;
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
