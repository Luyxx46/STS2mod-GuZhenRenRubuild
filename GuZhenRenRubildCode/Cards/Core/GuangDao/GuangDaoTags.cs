using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardTags;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 光道流派标签的唯一入口。
///
/// <para>
/// 「带光道标签」在本模组里等价于「这张普通牌自带折光段」：
/// 只有带标签的牌才能在类型交替（或强制折光）时触发折光。
/// 因此不带标签的普通牌天然就是<b>桥接牌</b>——
/// 正常把自己的类型提交进出牌历史，却不触发、也不消费强制折光标记。
/// （本模组当前的普通牌要么带光道标签、要么是蛊手牌固定位的系统牌，
/// 暂无桥接牌实例；原本充当该角色的玉皮甲已删除，判定机制保留备用。）
/// </para>
///
/// <para>
/// 蛊牌<b>不带</b>该标签：折光的判定对象是普通牌（伴生牌为主），
/// 蛊牌催动发生在蛊手牌层，不参与类型交替链。
/// </para>
/// </summary>
[RegisterOwnedCardTag(nameof(GuangDao))]
public sealed class GuangDaoTags
{
    /// <summary>光道标签；由 RitsuLib 的动态标签表按模组限定 ID 推导。</summary>
    public static readonly CardTag GuangDao = Create(nameof(GuangDao));

    private static CardTag Create(string localName) =>
        ModContentRegistry
            .GetQualifiedCardTagId(Entry.ModId, localName)
            .GetModCardTag();

    private GuangDaoTags()
    {
    }
}

/// <summary>光道标签的读取面，避免各处重复写 <c>Tags.Contains</c>。</summary>
public static class GuangDaoTagExtensions
{
    /// <summary>该牌是否带光道标签（即是否自带折光段）。</summary>
    public static bool IsGuangDaoCard(this CardModel? card) =>
        card != null && card.Tags.Contains(GuangDaoTags.GuangDao);
}
