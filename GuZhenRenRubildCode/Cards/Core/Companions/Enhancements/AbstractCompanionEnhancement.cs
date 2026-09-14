using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 强化槽内容的统一父类：一切「因为催动蛊牌而挂到伴生牌上」的强化都继承本类。
///
/// 原版只允许一张卡带一个附魔字段，因此本类不会直接占住
/// <see cref="CardModel.Enchantment"/>；实际承载者是
/// <see cref="CompanionEnhancementSlot"/>，它把本类的数值加成、战斗钩子与
/// 悬浮提示一起转发出去。子类可用的钩子集合 = 载体转发的那一批：
/// <list type="bullet">
/// <item>数值：<c>EnchantBlockAdditive/Multiplicative</c>、<c>EnchantDamageAdditive/Multiplicative</c>、<c>EnchantPlayCount</c>。</item>
/// <item>流程：<c>OnEnchant</c>、<c>RecalculateValues</c>、<c>OnPlay</c>、<c>AfterCardPlayed</c>、
/// <c>AfterCardDrawn</c>、<c>AfterAutoPrePlayPhaseEntered</c>、<c>BeforeFlush</c>、<c>ModifyShuffleOrder</c>。</item>
/// <item>显示：<c>ShouldGlowGold/Red</c>、<c>ShouldStartAtBottomOfDrawPile</c>、<c>HoverTips</c>。</item>
/// </list>
/// 需要卡面追加文本的子类自行把 <see cref="ModEnchantmentTemplate.HasExtraCardText"/> 覆盖成 true，
/// 并按 <c>GU_ZHEN_REN_RUBILD_ENCHANTMENT_&lt;类型名大写蛇形&gt;.extraCardText</c>
/// 写进中英 <c>enchantments.json</c>。
///
/// 需要自定义图标的子类把 PNG 放到 <c>GuZhenRenRubild/images/enchantments/&lt;类名&gt;.png</c>，
/// 并覆盖 <see cref="ModEnchantmentTemplate.CustomIconPath"/>（或
/// <see cref="ModEnchantmentTemplate.AssetProfile"/>）指向该文件；本基类不替子类猜路径，
/// 以免指向不存在的资源而干扰预加载。
/// </summary>
public abstract class AbstractCompanionEnhancement : ModEnchantmentTemplate
{
    // 一个强化槽只放一项强化，因此数量就是「层数」，卡面需要把它显示在强化图标上。
    public override bool ShowAmount => true;

    // 默认不追加卡面正文；需要展示实时数值的子类自行覆盖并补本地化 key。
    public override bool HasExtraCardText => false;

    // 强化只能由 CompanionEnhancementService 挂载，绝不参与原版附魔来源
    // （事件、遗物、篝火等），否则玩家会在与蛊牌无关的地方拿到强化。
    public override bool CanEnchant(CardModel card) => false;

    /// <summary>
    /// 本强化的叠加上限。挂载时会把层数限制在 <c>[1, MaxAmount]</c> 内；
    /// 默认不设上限，需要封顶的强化覆盖此属性。
    /// </summary>
    public virtual int MaxAmount => int.MaxValue;
}
