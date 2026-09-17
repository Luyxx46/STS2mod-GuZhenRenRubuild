using GuZhenRenRubild.Cards.Core.Abstractions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// B 形态（纯增幅）光道蛊的公共父类：蛊牌本身不承担攻防动作，
/// 催动只负责给伴生挂强化或改写折光规则，真正的动作全部交给伴生牌。
///
/// <para>
/// 催动增幅由 <c>GuZhenRenRubildRelic.BeforeCardPlayed</c> 在正式结算前统一挂载，
/// 因此这里默认什么都不做；需要额外改写规则的蛊（例如九转【光蛊】）覆写
/// <see cref="OnPlay"/> 即可。
/// </para>
/// </summary>
public abstract class AbstractGuangDaoSupportGuCard : AbstractGuCard
{
    protected AbstractGuangDaoSupportGuCard(
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    ) : base(type, rarity, target, showInCardLibrary)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => Task.CompletedTask;
}
