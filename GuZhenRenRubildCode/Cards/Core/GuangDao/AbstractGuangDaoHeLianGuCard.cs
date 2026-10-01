using GuZhenRenRubild.Cards.Core.Recipes;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 光道/月系合练结果蛊的公共父类。
///
/// <para>
/// 结果转数沿用基类的全局规则「<b>结果转数 = 材料最高转数 + 1</b>」：
/// 月霓裳蛊 2 = 月光蛊 1 + 1、光虹蛊 3 = 辉光蛊 2 + 1、聚光蛊 4 = 光虹蛊 3 + 1、
/// 恒光仙蛊 6 = 太光蛊 5 + 1……该规则现已统一写在
/// <see cref="AbstractGuCard.CalculateHeLianResultRank"/>，本父类不再单独覆盖。
/// </para>
///
/// <para>
/// 另外它们都是 B 形态纯增幅蛊，默认不产生直接攻防动作。
/// </para>
/// </summary>
public abstract class AbstractGuangDaoHeLianGuCard : AbstractHeLianGuCard
{
    protected AbstractGuangDaoHeLianGuCard(
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    ) : base(type, rarity, target, showInCardLibrary)
    {
    }

    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => Task.CompletedTask;
}
