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
/// 与既有合练蛊（月影蛊、玄铁蛊）的唯一差别是<b>结果转数策略</b>：
/// 本设计明确采用「结果转数 = 材料最高转数 + 1」
/// （光虹蛊 3 = 辉光蛊 2 + 1、聚光蛊 4 = 光虹蛊 3 + 1、恒光仙蛊 6 = 太光蛊 5 + 1……），
/// 而旧配方沿用「取最高转数」以保持已发布数值不变。两条策略各自独立，
/// 由本父类显式覆盖，不影响任何既有配方。
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
