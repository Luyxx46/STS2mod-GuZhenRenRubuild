using GuZhenRenRubild.Cards.Core.Runtime;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace GuZhenRenRubild.Cards.Core.Abstractions;

/// <summary>
/// 攻击型蛊牌的公共父类：出牌时按卡面当前伤害值攻击目标，
/// 目标非法（空目标或不能作为攻击目标）时不发动攻击。
///
/// 具体蛊牌只需声明基础数值、成长公式与可选的命中特效。
/// 合练蛊不能继承本类（已继承 <see cref="AbstractHeLianGuCard"/> 一系），
/// 但可以直接调用 <see cref="GuCardPlay.AttackAsync"/> 复用同一套逻辑。
/// </summary>
public abstract class AbstractGuAttackCard : AbstractGuCard
{
    /// <summary>命中特效资源路径，默认斩击。</summary>
    protected virtual string HitFxPath => GuCardPlay.DefaultAttackHitFx;

    protected AbstractGuAttackCard(
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
    ) => GuCardPlay.AttackAsync(this, choiceContext, cardPlay, HitFxPath);
}
