using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

using MultiEnchantmentMod.Api;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 「强化槽」的唯一写入入口。
///
/// <para>
/// 强化槽本身由前置 <b>MultiEnchantmentMod</b> 提供：该模组在原版唯一的
/// <see cref="CardModel.Enchantment"/> 之外旁路维护额外附魔槽，本模组不再自建复合载体，
/// 因此<b>原版附魔与催动蛊给予的强化天然共存</b>——原版附魔照旧占主槽（<c>card.Enchantment</c>），
/// 强化进额外槽，两者互不覆盖，也不必再做「摘下主槽 → 塞进载体 → 再放回」的中转。
/// </para>
///
/// <para>
/// 五条写入路径彼此独立，任何一条都不会覆盖或丢失另一项附魔：
/// <list type="bullet">
/// <item><see cref="Attach(CardModel, AbstractCompanionEnhancement, int)"/>：挂载强化；同类型按层叠加，
/// 层数统一夹进 <c>[1, <see cref="AbstractCompanionEnhancement.MaxAmount"/>]</c>，
/// 不同类型强化被拒绝 —— 一张卡只放一项强化。</item>
/// <item><see cref="Remove"/>：卸下强化，原版主槽附魔不受影响（额外槽与主槽彼此独立）。</item>
/// <item><see cref="ClearRegular"/>：只清原版附魔栏位，强化槽内容保留。</item>
/// <item><see cref="AttachToCompanionOf(CardModel, AbstractCompanionEnhancement, int)"/>：<b>催动蛊牌 → 给它的伴生牌挂强化</b> 的手动入口（定位牌组顺序最前的一张）。</item>
/// <item><see cref="ApplyDeclaredGrants(CardModel)"/>：<b>催动管线固定调用点</b>——蛊牌实现
/// <see cref="ICompanionEnhancementSourceGuCard"/> 声明授予（强化类型 + 自定义层数，形态对齐原版
/// <c>CardCmd.Enchant</c> 的自定义 amount，见 <see cref="CompanionEnhancementGrant"/>），
/// 催动时自动给配对内全部伴生牌挂载。</item>
/// </list>
/// </para>
///
/// <para>
/// 目标卡必须是 <see cref="ICompanionCard"/>（伴生牌）；这是本模组当前对强化槽的使用范围约束，
/// 框架本身对目标卡没有这个限制。
/// </para>
///
/// <para>
/// 落地方式统一走 <see cref="MultiEnchantmentApi.ForceEnchant"/>：它跳过
/// <see cref="EnchantmentModel.CanEnchant"/> 否决权（强化对该钩子恒返回 false，
/// 以免玩家从原版事件/遗物/篝火等来源拿到强化），但保留框架的叠层、作用域与通知语义。
/// </para>
/// </summary>
public static class CompanionEnhancementService
{
    // ---------------------------------------------------------------------
    // 查询
    // ---------------------------------------------------------------------

    /// <summary>卡牌当前是否带有强化（额外槽里的任意 <see cref="AbstractCompanionEnhancement"/>）。</summary>
    public static bool HasSlot(CardModel? card) => TryGetEnhancement(card) != null;

    /// <summary>
    /// 读取强化槽内容；没有强化时返回 null。
    /// 同一类型只可能有一个实例（登记为 <c>MergeAmount</c>），层数读其 <c>Amount</c>。
    /// </summary>
    public static AbstractCompanionEnhancement? TryGetEnhancement(CardModel? card) =>
        card == null
            ? null
            : MultiEnchantmentApi.GetEnchantment<AbstractCompanionEnhancement>(card);

    /// <summary>
    /// 读取原版附魔栏位内容：即 <see cref="CardModel.Enchantment"/> 本身。
    /// 强化已不再占用该字段，因此这里返回的就是原版附魔（没有则返回 null）。
    /// </summary>
    public static EnchantmentModel? TryGetRegular(CardModel? card) => card?.Enchantment;

    /// <summary>
    /// 按具体类型读取原版附魔栏位内容，供需要识别特定原版附魔的调用方使用。
    /// [预留] 当前无调用方：是为「按具体附魔类型分流」的适配预留的读取面。
    /// </summary>
    public static bool TryGetRegular<T>(CardModel? card, out T? regular)
        where T : EnchantmentModel
    {
        regular = TryGetRegular(card) as T;
        return regular != null;
    }

    /// <summary>
    /// 枚举卡上全部玩法附魔（原版附魔 + 强化），顺序由框架按「应用顺序」给出
    /// （主槽附魔在最前，随后是额外槽按附加先后排列）。没有附魔时返回空列表。
    /// </summary>
    public static IReadOnlyList<EnchantmentModel> EnumerateEnchantments(
        CardModel? card
    ) =>
        card == null
            ? Array.Empty<EnchantmentModel>()
            : MultiEnchantmentApi.GetEnchantments(card);

    // ---------------------------------------------------------------------
    // 写入：强化槽
    // ---------------------------------------------------------------------

    /// <summary>
    /// 给伴生牌挂载强化。返回实际生效的强化实例（已有同类型强化时就是那一项，层数已叠加），
    /// 目标不是伴生牌、实例已绑定其它卡、或槽内已有其它类型强化时返回 null 且不改动卡牌；
    /// 层数已达到 <see cref="AbstractCompanionEnhancement.MaxAmount"/> 时返回现有实例（幂等，不再叠加）。
    /// </summary>
    public static AbstractCompanionEnhancement? Attach(
        CardModel card,
        AbstractCompanionEnhancement enhancement,
        int amount
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(enhancement);

        // 已经绑定到某张卡的附魔不能再挂到第二张卡上：EnchantmentModel.ApplyInternal
        // 在 Card != null 时会抛 InvalidOperationException，而本入口的约定是
        // 「返回 null 且不改动卡牌」，所以必须提前拦下来。
        if (enhancement.HasCard)
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] card={card.Id} action=Reject " +
                $"incoming={enhancement.Id} reason=EnhancementAlreadyAttached"
            );
            return null;
        }

        if (!CanCarrySlot(card))
        {
            return null;
        }

        card.AssertMutable();

        // 一个槽只放一项：与已有强化类型不同时直接拒绝，绝不做静默替换。
        AbstractCompanionEnhancement? current = TryGetEnhancement(card);
        if (current != null && current.GetType() != enhancement.GetType())
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] card={card.Id} action=Reject " +
                $"incoming={enhancement.Id} existing={current.Id} " +
                "reason=SlotHoldsSingleEnhancement"
            );
            return null;
        }

        // 层数统一夹进 [1, MaxAmount]：首次挂载与后续叠加共用同一约定，
        // 授予方传多大都会被强化自身的上限封顶，不依赖具体子类自行防御。
        int currentAmount = current?.Amount ?? 0;
        int remaining = enhancement.MaxAmount - currentAmount;
        if (remaining <= 0)
        {
            return current;
        }

        int appliedAmount = Math.Clamp(amount, 1, remaining);

        // 跳过 CanEnchant 否决权（强化恒为 false），叠层/作用域/通知仍由框架处理；
        // 同类型已在卡上时框架走 MergeAmount 合并分支，返回的是那个既有实例。
        EnchantmentModel? applied = MultiEnchantmentApi.ForceEnchant(
            card,
            enhancement,
            appliedAmount
        );

        return applied as AbstractCompanionEnhancement;
    }

    /// <summary>按类型挂载强化，内部会从 <see cref="ModelDb"/> 取规范实例的可变副本。</summary>
    public static AbstractCompanionEnhancement? Attach<T>(
        CardModel card,
        int amount = 1
    ) where T : AbstractCompanionEnhancement
    {
        ArgumentNullException.ThrowIfNull(card);

        // 先做目标校验再创建可变副本：非伴生牌时直接返回，不白造一个克隆再丢弃。
        if (!CanCarrySlot(card))
        {
            return null;
        }

        return Attach(card, (T)ModelDb.Enchantment<T>().ToMutable(), amount);
    }

    /// <summary>
    /// 卸下强化槽内容。原版附魔栏位不受影响（两者本就分处主槽与额外槽）；
    /// 返回是否真的卸下了强化（槽为空时返回 false 且不改动卡牌）。
    /// </summary>
    public static bool Remove(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        IReadOnlyList<AbstractCompanionEnhancement> existing =
            MultiEnchantmentApi.GetEnchantments<AbstractCompanionEnhancement>(card);
        if (existing.Count == 0)
        {
            return false;
        }

        card.AssertMutable();

        bool removed = false;
        foreach (AbstractCompanionEnhancement enhancement in existing)
        {
            removed |= MultiEnchantmentApi.RemoveEnchantment(
                card,
                enhancement,
                RemovalReason.Manual
            );
        }

        return removed;
    }

    /// <summary>
    /// 只清空原版附魔栏位，保留强化槽内容。
    /// 返回是否真的清掉了原版附魔（本来就没有时返回 false 且不改动卡牌）。
    /// </summary>
    public static bool ClearRegular(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.Enchantment == null)
        {
            return false;
        }

        card.AssertMutable();
        CardCmd.ClearEnchantment(card);
        return true;
    }

    // ---------------------------------------------------------------------
    // 写入：催动蛊牌 → 伴生牌
    // ---------------------------------------------------------------------

    /// <summary>
    /// 给「该蛊牌的伴生牌」挂载强化（定位牌组顺序最前的一张）：
    /// 内部通过 <see cref="CompanionRelationshipService.TryGetCompanion"/> 定位伴生牌，
    /// 找不到（不在牌组、未建立配对、非伴生来源蛊）时返回 null 并只记一条警告，不抛异常。
    /// </summary>
    public static AbstractCompanionEnhancement? AttachToCompanionOf(
        CardModel sourceGu,
        AbstractCompanionEnhancement enhancement,
        int amount
    )
    {
        ArgumentNullException.ThrowIfNull(sourceGu);
        ArgumentNullException.ThrowIfNull(enhancement);

        if (!CompanionRelationshipService.TryGetCompanion(
                sourceGu,
                out CardModel companion
            ))
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] source={sourceGu.Id} action=Skip " +
                "reason=NoCompanion"
            );
            return null;
        }

        return Attach(companion, enhancement, amount);
    }

    /// <summary>按类型给该蛊牌的伴生牌挂载强化，找不到伴生牌时返回 null。</summary>
    public static AbstractCompanionEnhancement? AttachToCompanionOf<T>(
        CardModel sourceGu,
        int amount = 1
    ) where T : AbstractCompanionEnhancement =>
        AttachToCompanionOf(
            sourceGu,
            (T)ModelDb.Enchantment<T>().ToMutable(),
            amount
        );

    /// <summary>
    /// 按授予声明给「该蛊牌的全部伴生牌」挂载强化：多伴生来源
    /// （<see cref="CompanionDefinition.Count"/> &gt; 1）的每一张都会独立拿到层数。
    /// 伴生牌通过 <see cref="CompanionRelationshipService.GetCompanions"/> 解析为
    /// 永久牌组实例，因此战斗中催动同样落在持久侧，存档与联机天然同步。
    ///
    /// <para>
    /// 返回每张伴生牌实际生效的强化实例（挂载被拒或找不到伴生牌的项不出现在结果里）；
    /// 全程只记警告不抛异常——授予是增强行为，绝不打断催动流程。
    /// </para>
    /// </summary>
    public static IReadOnlyList<AbstractCompanionEnhancement> AttachToCompanionsOf(
        CardModel sourceGu,
        CompanionEnhancementGrant grant
    )
    {
        ArgumentNullException.ThrowIfNull(sourceGu);
        ArgumentNullException.ThrowIfNull(grant);

        IReadOnlyList<CardModel> companions =
            CompanionRelationshipService.GetCompanions(sourceGu);
        if (companions.Count == 0)
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] source={sourceGu.Id} action=Skip " +
                "reason=NoCompanion"
            );
            return [];
        }

        // 先解析一次：类型校验与注册校验只做一回，失败只记一条警告。
        if (ResolveGrant(sourceGu.Id, grant) is not { } template)
        {
            return [];
        }

        List<AbstractCompanionEnhancement> attached = [];
        foreach (CardModel companion in companions)
        {
            // 每张伴生牌挂独立的可变克隆：附魔实例绑定第一张卡后
            // （HasCard=true）不能再挂第二张，Attach 会直接拒绝；
            // 克隆体由 DeepCloneFields 清空卡绑定，可以安全落到下一张。
            var enhancement =
                (AbstractCompanionEnhancement)
                template.ClonePreservingMutability();

            if (Attach(companion, enhancement, grant.Amount) is not { } applied)
            {
                continue;
            }

            attached.Add(applied);
        }

        return attached;
    }

    /// <summary>
    /// 催动管线的固定调用点：按蛊牌自身的声明给它的伴生牌挂强化。
    ///
    /// <para>
    /// 蛊牌实现 <see cref="ICompanionEnhancementSourceGuCard"/> 并在
    /// <see cref="ICompanionEnhancementSourceGuCard.BuildCompanionEnhancementGrant"/>
    /// 里声明「给什么强化、本次几层」即可，无需写任何挂载代码；
    /// 未实现接口的蛊牌零开销直接返回，声明返回 null（条件授予不触发）时同样无操作。
    /// 返回本次实际挂载成功的强化列表（每张伴生牌一项）。
    /// </para>
    /// </summary>
    public static IReadOnlyList<AbstractCompanionEnhancement> ApplyDeclaredGrants(
        CardModel activatedGu
    )
    {
        ArgumentNullException.ThrowIfNull(activatedGu);

        if (activatedGu is not ICompanionEnhancementSourceGuCard declaration)
        {
            return [];
        }

        CompanionEnhancementGrant? grant =
            declaration.BuildCompanionEnhancementGrant();
        if (grant == null)
        {
            return [];
        }

        return AttachToCompanionsOf(activatedGu, grant);
    }

    // ---------------------------------------------------------------------
    // 内部
    // ---------------------------------------------------------------------

    /// <summary>
    /// 把授予声明解析成一个全新的可变强化实例：校验类型归属与注册状态，
    /// 解析失败时记警告并返回 null，绝不抛异常（授予是增强行为，不该打断催动）。
    /// </summary>
    private static AbstractCompanionEnhancement? ResolveGrant(
        ModelId sourceId,
        CompanionEnhancementGrant grant
    )
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (!typeof(AbstractCompanionEnhancement)
                .IsAssignableFrom(grant.EnhancementType))
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] source={sourceId} action=Skip " +
                $"grant={grant.EnhancementType.Name} " +
                "reason=NotCompanionEnhancement"
            );
            return null;
        }

        if (ModelDb.GetByIdOrNull<EnchantmentModel>(
                ModelDb.GetId(grant.EnhancementType)
            ) is not AbstractCompanionEnhancement canonical)
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] source={sourceId} action=Skip " +
                $"grant={grant.EnhancementType.Name} reason=NotRegistered"
            );
            return null;
        }

        return (AbstractCompanionEnhancement)canonical.ToMutable();
    }

    private static bool CanCarrySlot(CardModel card)
    {
        if (card is ICompanionCard)
        {
            return true;
        }

        Entry.Logger.Warn(
            $"[Companion/Enhancement] card={card.Id} action=Skip " +
            "reason=NotCompanionCard"
        );
        return false;
    }
}
