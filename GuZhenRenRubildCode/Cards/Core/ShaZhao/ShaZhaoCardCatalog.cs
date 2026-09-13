using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 杀招内容目录：本模组所有"按类型找杀招"的查询都集中在这里，
/// 与蛊牌的 <c>GuCardCatalog</c> 一一对应，避免各处重复书写
/// <see cref="ModelDb"/> + 杀招专属卡池的固定组合。
///
/// 池中保存的是规范实例（canonical），要放进战斗或界面必须先由
/// CombatState 创建或转成可变副本，因此这里同时提供这两个入口。
/// </summary>
public static class ShaZhaoCardCatalog
{
    /// <summary>杀招专属卡池中的全部规范卡牌。</summary>
    public static IEnumerable<CardModel> AllCards =>
        ModelDb.CardPool<GuZhenRenRubildShaZhaoCardPool>().AllCards;

    /// <summary>杀招池当前是否已经注册了至少一张杀招牌。</summary>
    public static bool HasAnyCard => AllCards.Any();

    /// <summary>
    /// 按类型取出池中的规范实例；类型未注册时抛出异常，
    /// 便于在开发期立刻发现拼错的类型或漏注册的杀招。
    /// </summary>
    public static CardModel FindCanonical(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        return AllCards.Single(card => card.GetType() == cardType);
    }

    /// <summary>
    /// 按类型尝试取出规范实例，不抛异常。
    ///
    /// 委托给 <see cref="Catalog.GuCardCatalog.TryFindCanonical"/> 跨全部模组卡池查找，
    /// 这样界面从杀招跳到材料蛊、或从蛊跳到伴生牌都能取到规范实例。
    /// </summary>
    public static bool TryFindCanonical(
        Type cardType,
        out CardModel? canonical
    )
    {
        ArgumentNullException.ThrowIfNull(cardType);

        return Catalog.GuCardCatalog.TryFindCanonical(cardType, out canonical);
    }

    /// <summary>
    /// 为选择界面创建一张带归属的预览用可变副本。
    /// 预览不进入战斗状态，因此不需要 CombatState 创建。
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
