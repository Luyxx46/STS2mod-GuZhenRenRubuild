using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 强化槽内容的统一父类：一切「因为催动蛊牌而挂到伴生牌上」的强化都继承本类。
///
/// <para>
/// 槽位由前置 <b>MultiEnchantmentMod</b> 提供的旁路额外附魔槽承载，
/// 本类实例经 <see cref="CompanionEnhancementService"/> 挂到额外槽，
/// <b>不占用</b> <see cref="CardModel.Enchantment"/>（原版主槽）。
/// 因此同一张牌可以同时拥有原版附魔与另一张牌给出的强化，两者互不覆盖。
/// </para>
///
/// <para>
/// 子类约定：
/// <list type="bullet">
/// <item>必须标 RitsuLib 的 <c>[RegisterEnchantment]</c>，才能作为模型进入 <c>ModelDb</c>；
/// 否则 <see cref="CompanionEnhancementService.Attach{T}"/> 与授予声明都会解析失败。</item>
/// <item><b>不要</b>标 MultiEnchantmentMod 的 <c>[Enchantment]</c>：叠层语义由
/// <see cref="CompanionEnhancementRegistration"/> 在初始化时集中登记
/// （<c>MergeAmount</c> + <c>SharedAcrossStack</c>，即「一个实例 + 层数」）。</item>
/// <item>数值与流程钩子沿用原版 <see cref="EnchantmentModel"/> 的可覆写成员
/// （<c>EnchantBlockAdditive/Multiplicative</c>、<c>EnchantDamageAdditive/Multiplicative</c>、
/// <c>EnchantPlayCount</c>、<c>OnPlay</c>、<c>RecalculateValues</c>、
/// <c>AfterCardPlayed</c>、<c>AfterCardDrawn</c>、<c>ShouldGlowGold/Red</c>、
/// <c>ShouldStartAtBottomOfDrawPile</c>、<c>HoverTips</c> 等），
/// 由前置以与主槽附魔一致的方式分发；此外前置还提供作用域、合并回调、关键词发射等
/// 自有生命周期钩子，需要时查阅其 <c>Api/</c> 文档。</item>
/// </list>
/// </para>
///
/// <para>
/// 需要卡面追加文本的子类自行把 <see cref="ModEnchantmentTemplate.HasExtraCardText"/> 覆盖成 true，
/// 并按 <c>GU_ZHEN_REN_RUBILD_ENCHANTMENT_&lt;类型名大写蛇形&gt;.extraCardText</c>
/// 写进中英 <c>enchantments.json</c>。
/// </para>
///
/// <para>
/// 需要自定义图标的子类把 PNG 放到 <c>GuZhenRenRubild/images/enchantments/&lt;类名&gt;.png</c>，
/// 并覆盖 <see cref="ModEnchantmentTemplate.CustomIconPath"/>（或
/// <see cref="ModEnchantmentTemplate.AssetProfile"/>）指向该文件；本基类不替子类猜路径，
/// 以免指向不存在的资源而干扰预加载。
/// </para>
/// </summary>
public abstract class AbstractCompanionEnhancement : ModEnchantmentTemplate
{
    // 一个强化类型在卡上只有一个实例，层数记在 Amount 上，卡面需要把它显示在强化图标上。
    public override bool ShowAmount => true;

    // 默认不追加卡面正文；需要展示实时数值的子类自行覆盖并补本地化 key。
    public override bool HasExtraCardText => false;

    // 强化只能由 CompanionEnhancementService 挂载，绝不参与原版附魔来源
    // （事件、遗物、篝火等），否则玩家会在与蛊牌无关的地方拿到强化。
    //
    // 这个 false 不会阻断本模组自己的挂载：挂载统一走
    // MultiEnchantmentApi.ForceEnchant，它在单次调用内跳过该否决权，
    // 叠层与作用域语义不受影响。
    public override bool CanEnchant(CardModel card) => false;

    /// <summary>
    /// 本强化的叠加上限。挂载时会把层数限制在 <c>[1, MaxAmount]</c> 内；
    /// 默认不设上限，需要封顶的强化覆盖此属性。
    /// </summary>
    public virtual int MaxAmount => int.MaxValue;
}
