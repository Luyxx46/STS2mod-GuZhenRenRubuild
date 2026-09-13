using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Runtime;

/// <summary>
/// 蛊牌与伴生牌的通用出牌动作。
///
/// 攻击和格挡是绝大多数卡牌唯一的实际效果，这里把"目标合法性判定 +
/// 伤害/格挡命令绑定"固定成两个入口：卡牌类只声明数值与成长公式，
/// 不再各自复制一遍命令链。合练蛊、伴生牌等无法共用父类的卡牌也能直接复用。
/// </summary>
public static class GuCardPlay
{
    /// <summary>默认的斩击命中特效。</summary>
    public const string DefaultAttackHitFx = "vfx/vfx_attack_slash";

    /// <summary>
    /// 以卡面当前伤害值发动一次攻击。
    /// 联机与自动流程可能传入空目标，此时不发动攻击。
    /// </summary>
    public static async Task AttackAsync(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        string hitFxPath
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        if (cardPlay.Target == null || !card.IsValidTarget(cardPlay.Target))
        {
            return;
        }

        await DamageCmd
            .Attack(card.DynamicVars.Damage.BaseValue)
            .FromCard(card, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(hitFxPath)
            .Execute(choiceContext);
    }

    /// <summary>以卡面当前格挡值获得格挡。</summary>
    public static Task GainBlockAsync(CardModel card, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(card);

        return CreatureCmd.GainBlock(
            card.Owner.Creature,
            card.DynamicVars.Block,
            cardPlay
        );
    }
}
