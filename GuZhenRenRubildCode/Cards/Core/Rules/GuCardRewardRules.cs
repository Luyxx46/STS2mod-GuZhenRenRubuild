using GuZhenRenRubild.Cards;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Rules;

public static class GuCardRewardRules
{
    public static bool CanAppear(Player player, CardModel candidate)
    {
        return player.Character is not GuZhenRenRubildCharacter ||
            candidate is IGuCard;
    }
}
