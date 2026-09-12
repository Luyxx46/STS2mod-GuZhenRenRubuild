using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace GuZhenRenRubild.Cards.Core;

/// <summary>
/// 防御型蛊牌的公共父类：声明本卡会获得格挡，并在出牌时按卡面当前
/// 格挡值结算。具体蛊牌只需声明基础数值与成长公式。
///
/// 合练蛊不能继承本类（已继承 <see cref="AbstractHeLianGuCard"/> 一系），
/// 但可以直接调用 <see cref="GuCardPlay.GainBlockAsync"/> 复用同一套逻辑；
/// 伴生牌同样如此（它们继承的是普通卡牌体系）。
/// </summary>
public abstract class AbstractGuBlockCard : AbstractGuCard
{
    // 告诉游戏该卡会获得格挡，供提示、统计及其他规则系统识别。
    public override bool GainsBlock => true;

    protected AbstractGuBlockCard(
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
    ) => GuCardPlay.GainBlockAsync(this, cardPlay);
}
