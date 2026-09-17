using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Core.Runtime;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 光道/月系合练结果蛊的公共父类。
///
/// <para>
/// 结果转数策略固定为「<b>结果转数 = 材料最高转数 + 1</b>」：
/// 光虹蛊 3 = 辉光蛊 2 + 1、聚光蛊 4 = 光虹蛊 3 + 1、恒光仙蛊 6 = 太光蛊 5 + 1……
/// 这是设计案对这一批配方的明确规则；覆盖只作用在本父类的子类上，
/// 其它任何配方（含基础父类 <see cref="AbstractHeLianGuCard"/> 的默认「取最高转数」）都不受影响。
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

    /// <summary>结果转数 = 材料最高转数 + 1，再夹进本蛊自身的转数窗口。</summary>
    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        int highest = materials
            .OfType<IGuCard>()
            .Select(gu => Math.Max(MinimumGuRank, gu.GuRank))
            .DefaultIfEmpty(MinimumGuRank)
            .Max();

        return Math.Clamp(highest + 1, MinimumGuRank, MaxGuRank);
    }

    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => Task.CompletedTask;
}
