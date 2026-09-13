using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Catalog;

/// <summary>
/// 蛊牌内容目录：本模组所有"按类型找蛊牌"的查询都集中在这里，
/// 避免各处重复书写 <see cref="ModelDb"/> + 蛊牌主奖励池的固定组合。
///
/// 池中保存的是规范实例（canonical），要放进牌组或界面必须先由
/// RunState 创建或转成可变副本，因此这里同时提供这两个入口。
/// </summary>
public static class GuCardCatalog
{
    /// <summary>
    /// 本模组全部卡池：蛊牌主池、杀招池，以及收普通牌与伴生牌的辅助池。
    ///
    /// 界面（蛊方大全）经常要跨池找牌——例如从某只蛊的查看页跳到它的伴生牌，
    /// 而伴生牌不在蛊牌池里。凡是"按类型找一张本模组的牌"都应该走
    /// <see cref="TryFindCanonical"/>，它会把三个池都扫一遍。
    /// </summary>
    public static IEnumerable<CardModel> AllModCards =>
        ModelDb.CardPool<GuZhenRenRubildGuCardPool>().AllCards
            .Concat(ModelDb.CardPool<GuZhenRenRubildShaZhaoCardPool>().AllCards)
            .Concat(ModelDb.CardPool<GuZhenRenRubildCardPool>().AllCards);

    /// <summary>蛊牌主奖励池中的全部规范卡牌（含合练结果蛊）。</summary>
    public static IEnumerable<CardModel> AllCards =>
        ModelDb.CardPool<GuZhenRenRubildGuCardPool>().AllCards;

    /// <summary>
    /// 按卡牌类型取出池中的规范实例；类型未注册时抛出异常，
    /// 便于在开发期立刻发现拼错的类型或漏注册的卡牌。
    /// </summary>
    public static CardModel FindCanonical(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        return AllCards.Single(card => card.GetType() == cardType);
    }

    /// <summary>
    /// 按类型尝试取出规范实例，不抛异常。跨全部模组卡池查找，
    /// 因此伴生牌一类"不在蛊牌池里"的卡也能取到。
    /// </summary>
    public static bool TryFindCanonical(
        Type cardType,
        out CardModel? canonical
    )
    {
        ArgumentNullException.ThrowIfNull(cardType);

        canonical = AllModCards.FirstOrDefault(
            card => card.GetType() == cardType
        );
        return canonical != null;
    }

    /// <summary>
    /// 为选择界面创建一张带归属的预览用可变副本。
    /// 预览不进入运行状态，因此不需要 RunState 创建。
    /// </summary>
    public static CardModel CreateOwnedPreview(
        Type cardType,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        CardModel preview = FindCanonical(cardType).ToMutable();
        preview.Owner = owner;
        return preview;
    }
}
