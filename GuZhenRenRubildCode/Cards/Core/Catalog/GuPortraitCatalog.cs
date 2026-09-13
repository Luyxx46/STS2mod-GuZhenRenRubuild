using Godot;

using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Catalog;

/// <summary>
/// 蛊虫贴图目录：蛊方大全只插入卡牌贴图，不再渲染原生卡面，
/// 因此所有"取一张卡的图"都集中走这里。
///
/// 贴图来自 <see cref="CardModel.Portrait"/>. 该属性在
/// <c>ModCardTemplate</c> 子类里已被 RitsuLib 覆写为
/// <c>CardAssetProfile.PortraitPath</c>，因此本模组卡牌拿到的是
/// <c>res://GuZhenRenRubild/images/...</c> 下的 PNG，而不是原版图集路径。
///
/// 单独抽一层的意义：以后某只蛊换成专属蛊图时，只需要改卡牌自身的
/// <c>AssetProfile</c>，界面代码一行都不用动。
/// </summary>
internal static class GuPortraitCatalog
{
    /// <summary>
    /// 取一张卡牌的贴图。取不到时返回 null 并记一条警告，
    /// 由界面退化成"无图 + 文字"，不让图鉴因此报错。
    /// </summary>
    internal static Texture2D? TryGetPortrait(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        try
        {
            Texture2D? texture = card.Portrait;

            if (texture != null && GodotObject.IsInstanceValid(texture))
            {
                return texture;
            }

            Entry.Logger.Warn(
                $"蛊方大全：卡牌 {card.Id} 没有可用贴图。"
            );
            return null;
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"蛊方大全：加载卡牌 {card.Id} 贴图失败：{exception.Message}"
            );
            return null;
        }
    }
}
