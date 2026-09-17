using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using MultiEnchantmentMod.Api;

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
/// <b>生命周期与可用次数</b>（服务层挂载时统一施加 <c>EnchantmentScope.UntilCombatEnds</c>）：
/// <list type="bullet">
/// <item>强化只活在<b>当前这场战斗</b>内：战斗结束由前置自动清除，也不会被镜像进牌组；
/// 下一场战斗需要由父蛊<b>重新催动授予</b>。</item>
/// <item><see cref="UsesPerCombat"/> 声明「每场战斗可触发几次」（0 = 不限）。
/// 次数记在按战斗卡分账的 <see cref="CompanionEnhancementUses"/> 上，
/// 每次<b>真正触发</b>消耗一次；耗尽时本强化被整体移除，卡面图标随之消失
/// （即「清空强化槽」）。同一场战斗内重新授予不会回满次数。</item>
/// <item>层数（<c>Amount</c>，由父蛊多次催动叠加）与次数是两个独立维度：
/// 层数是强度，次数是可用次数。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>子类约定</b>：
/// <list type="bullet">
/// <item>必须标 RitsuLib 的 <c>[RegisterEnchantment]</c>，才能作为模型进入 <c>ModelDb</c>；
/// 否则 <see cref="CompanionEnhancementService.Attach{T}"/> 与授予声明都会解析失败。</item>
/// <item><b>不要</b>标 MultiEnchantmentMod 的 <c>[Enchantment]</c>：叠层语义由
/// <see cref="CompanionEnhancementRegistration"/> 集中登记
/// （<c>MergeAmount</c> + <c>SharedAcrossStack</c> + <c>OnPlay = PerLiveInstance</c>）。</item>
/// <item><b>不要覆写 <see cref="OnPlay"/></b>（本类已 sealed：次数消耗必须只此一条路径）。
/// 需要「真正触发时才计数」的条件判断，覆写 <see cref="IsEffectTriggered"/>；
/// 需要在触发瞬间做别的事，覆写 <see cref="OnEffectTriggered"/>。</item>
/// <item>数值与流程钩子沿用原版 <see cref="EnchantmentModel"/> 的可覆写成员
/// （<c>EnchantBlockAdditive/Multiplicative</c>、<c>EnchantDamageAdditive/Multiplicative</c>、
/// <c>EnchantPlayCount</c>、<c>RecalculateValues</c>、<c>ShouldGlowGold/Red</c>、
/// <c>ShouldStartAtBottomOfDrawPile</c>、<c>HoverTips</c> 等），由前置以与主槽附魔一致的方式分发。
/// <b>注意原版钩子契约</b>：加值类钩子返回的是<b>增量</b>而不是「原值 + 增量」
/// （分发侧是 <c>result += 钩子(...)</c>），返回原值会双倍计算。</item>
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
    /// 强化图标：把 <c>GuZhenRenRubild/images/enchantments/&lt;类名&gt;.png</c>
    /// 放进资源目录即自动生效；缺失时返回 null（不覆盖，沿用引擎默认图标）。
    /// </summary>
    public override string? CustomIconPath =>
        ModAssetPathResolver.ResolveOptional(
            $"{Entry.ResPath}/images/enchantments/{GetType().Name}.png",
            null
        );

    /// <summary>
    /// 本强化的叠加上限（层数上限）。挂载时会把层数限制在 <c>[1, MaxAmount]</c> 内；
    /// 默认不设上限，需要封顶的强化覆盖此属性。
    /// </summary>
    public virtual int MaxAmount => int.MaxValue;

    /// <summary>
    /// 本强化<b>每场战斗</b>可触发的次数；<c>0</c> 表示不限次数（活到战斗结束）。
    /// 由强化自身声明，同一强化全局一致。
    ///
    /// <para>
    /// 授予方（父蛊）可以用 <c>CompanionEnhancementGrant.UsesPerCombat</c> 按转数覆写它
    /// ——例如恒照在六/七/八转分别是 1/2/3 次。覆写值记录在
    /// <see cref="GrantedUsesPerCombat"/> 上，实际生效值见 <see cref="EffectiveUsesPerCombat"/>。
    /// </para>
    /// </summary>
    public virtual int UsesPerCombat => 0;

    /// <summary>
    /// 本次授予按转数覆写的每场可用次数；<c>null</c> 表示沿用 <see cref="UsesPerCombat"/>。
    /// 由 <see cref="CompanionEnhancementService"/> 在挂载前写入。
    /// </summary>
    public int? GrantedUsesPerCombat { get; internal set; }

    /// <summary>
    /// 本次授予按转数给出的数值标量（例如「折光格挡额外 +X」里的 X）。
    /// 层数（<see cref="EnchantmentModel.Amount"/>）只负责卡面显示与叠层，
    /// 数值必须走这里，两条轴不能混用。由服务层在挂载前写入。
    /// </summary>
    public int GrantedMagnitude { get; internal set; }

    /// <summary>实际生效的每场可用次数（0 = 不限）。</summary>
    public int EffectiveUsesPerCombat => GrantedUsesPerCombat ?? UsesPerCombat;

    /// <summary>
    /// 本强化是否只在<b>子卡真正触发折光</b>的那次出牌才消耗可用次数。
    /// 光道伴生强化全部覆盖成 true，避免「打出但没折光」白扣次数。
    /// </summary>
    protected virtual bool RequiresRefractionTrigger => false;

    // 本次出牌里子卡是否已经通知过「折光触发」。
    // 由 GuangDaoCardPlay 在折光段真正结算时登记，OnPlay 派发后立刻清掉。
    private bool _refractionTriggered;

    /// <summary>由卡面的折光段入口调用：登记「本次出牌真的折光了」。</summary>
    public void MarkRefractionTriggered() => _refractionTriggered = true;

    /// <summary>
    /// 本场战斗剩余可用次数。<see cref="EffectiveUsesPerCombat"/> 为 0 时返回 <c>int.MaxValue</c>
    /// （不限次数）；尚未记账时返回声明的完整次数。
    /// </summary>
    public int RemainingUses
    {
        get
        {
            int declared = EffectiveUsesPerCombat;
            if (declared <= 0)
            {
                return int.MaxValue;
            }

            CardModel? card = Card;
            return card == null
                ? declared
                : CompanionEnhancementUses.Peek(card, GetType()) ?? declared;
        }
    }

    /// <summary>
    /// 本次打出是否<b>真正触发</b>了本强化的效果 —— 只有返回 true 才消耗一次可用次数。
    ///
    /// <para>
    /// 非虚：次数语义只有「打出即触发」与「折光才触发」两种，分别由
    /// <see cref="RequiresRefractionTrigger"/> 与卡面的折光段入口决定，
    /// 子类不再自行覆写，避免出现绕过次数记账的分支。
    /// </para>
    /// </summary>
    private bool IsEffectTriggered() =>
        !RequiresRefractionTrigger || _refractionTriggered;

    /// <summary>
    /// 效果真正触发时的附加行为（默认什么都不做）。
    /// 需要「触发瞬间顺带做点什么」的强化覆写这里，而不是覆写 <see cref="OnPlay"/>。
    /// </summary>
    protected virtual Task OnEffectTriggered(
        PlayerChoiceContext choiceContext,
        CardPlay? cardPlay
    ) => Task.CompletedTask;

    /// <summary>
    /// 逐次消耗可用次数的唯一入口。前置对额外槽的 <c>OnPlay</c> 派发发生在
    /// <b>子卡自身效果结算之后</b>，因此在这里扣减既能保证最后一次触发仍吃满效果，
    /// 也能在耗尽时立刻清空强化槽（卡面图标随之消失）。
    ///
    /// <para>sealed：子类若覆写 OnPlay 就会绕过次数消耗，破坏「每场战斗 N 次」的约定。</para>
    /// </summary>
    public sealed override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay? cardPlay
    )
    {
        CardModel? card = Card;

        // 标记是一次性的：无论本次是否触发，派发结束后都要清掉，
        // 否则上一次的折光会泄漏成下一次的「已触发」。
        bool triggered = card != null && IsEffectTriggered();
        _refractionTriggered = false;

        if (!triggered || card == null)
        {
            return;
        }

        await OnEffectTriggered(choiceContext, cardPlay);

        int declared = EffectiveUsesPerCombat;
        if (declared <= 0)
        {
            return;
        }

        if (CompanionEnhancementUses.Consume(card, GetType()) > 0)
        {
            return;
        }

        // 次数耗尽：整项移除（清空强化槽）。
        MultiEnchantmentApi.RemoveEnchantment(
            card,
            this,
            RemovalReason.ActivationLimitReached
        );
    }
}
