using GuZhenRenRubild.Cards.Core.GuangDao;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.HeLian;

/// <summary>
/// 【恒照】的重复收益收口：两张恒光伴生共用同一段逻辑。
///
/// <para>
/// 只有同时满足「本次折光真的被聚光重复过（<paramref name="passes"/> ≥ 2）」
/// 与「本牌身上确实还有恒照强化」时才生效；返还 1 聚光并使下一张拥有折光段的牌强制折光。
/// 次数由强化自身的每场可用次数封顶，因此不会无限传递。
/// </para>
/// </summary>
internal static class HengZhaoRefractionReward
{
    internal static async Task ApplyAsync(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passes
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        if (passes < 2 ||
            GuangDaoCardPlay.ReadEnhancement(card)
                ?.RewardsRepeatedRefraction != true)
        {
            return;
        }

        await GuangDaoSystem.GrantJuGuangAsync(choiceContext, card, 1);
        GuangDaoSystem.ForceNextRefraction(
            card.Owner,
            GuangDaoForceScope.AnyGuangDaoCard
        );
    }
}
