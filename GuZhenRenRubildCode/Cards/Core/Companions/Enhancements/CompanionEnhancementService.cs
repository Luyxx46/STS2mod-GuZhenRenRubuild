using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 「强化槽」的唯一写入入口。
///
/// <para>
/// 四条写入路径彼此独立，任何一条都不会覆盖或丢失另一项附魔：
/// <list type="bullet">
/// <item><see cref="Attach(CardModel, AbstractCompanionEnhancement, int)"/>：挂载强化；卡上原本没有附魔时强化直接占字段，
/// 已有原生附魔时才建立 <see cref="CompanionEnhancementSlot"/> 载体把两者一起保存。同类型强化按层叠加（上限 <see cref="AbstractCompanionEnhancement.MaxAmount"/>），
/// 不同类型强化被拒绝 —— 一个槽只放一项。</item>
/// <item><see cref="Remove"/>：卸下强化；若卡上还有原生附魔，会把它还原成直接挂在卡上的形态（而不是吞掉）。</item>
/// <item><see cref="ClearRegular"/>：只清原生附魔，保留强化槽内容。</item>
/// <item><see cref="AttachToCompanionOf(CardModel, AbstractCompanionEnhancement, int)"/>：<b>催动蛊牌 → 给它的伴生牌挂强化</b> 的唯一入口。</item>
/// </list>
/// </para>
///
/// 目标卡必须是 <see cref="ICompanionCard"/>（伴生牌）；这是本模组当前对强化槽的使用范围约束，
/// 载体本身并不依赖它。
/// </summary>
public static class CompanionEnhancementService
{
    // ---------------------------------------------------------------------
    // 查询
    // ---------------------------------------------------------------------

    /// <summary>卡牌当前是否由强化槽载体承载附魔。</summary>
    public static bool HasSlot(CardModel? card) =>
        card?.Enchantment is CompanionEnhancementSlot;

    /// <summary>
    /// 读取强化槽内容；没有载体或槽为空时返回 null。
    /// [预留] 当前无调用方：留给后续具体强化读取自身层数等用途。
    /// </summary>
    public static AbstractCompanionEnhancement? TryGetEnhancement(CardModel? card) =>
        card?.Enchantment is CompanionEnhancementSlot slot
            ? slot.Enhancement
            : null;

    /// <summary>读取原生附魔栏位内容；没有载体时返回 null（此时原生附魔直接挂在卡上）。</summary>
    public static EnchantmentModel? TryGetRegular(CardModel? card) =>
        card?.Enchantment is CompanionEnhancementSlot slot ? slot.Regular : null;

    /// <summary>
    /// 按具体类型读取原生附魔栏位内容，供需要识别特定原版附魔的调用方使用。
    /// [预留] 当前无调用方：是为「按具体附魔类型分流」的适配预留的读取面
    /// （非泛型的 <see cref="TryGetRegular(CardModel?)"/> 才是补丁实际使用的那一个）。
    /// </summary>
    public static bool TryGetRegular<T>(CardModel? card, out T? regular)
        where T : EnchantmentModel
    {
        regular = TryGetRegular(card) as T;
        return regular != null;
    }

    /// <summary>
    /// 枚举卡上全部附魔（原生 + 强化），顺序固定为「原生附魔 → 强化」。
    /// [预留] 当前无调用方：这是留给后续具体强化的公开对称读取面。
    ///
    /// 补丁目前不经过这里，而是直接读载体字段（<c>CompanionEnhancementSlot.Regular</c> /
    /// <c>Enhancement</c> / <c>InnerEnchantments</c>）：追加卡面正文与拼悬浮提示时
    /// 需要区分「这是原生附魔还是强化」，而不只是按顺序遍历全部内层。
    /// </summary>
    public static IReadOnlyList<EnchantmentModel> EnumerateEnchantments(
        CardModel? card
    )
    {
        if (card?.Enchantment is CompanionEnhancementSlot slot)
        {
            return slot.InnerEnchantments;
        }

        return card?.Enchantment is { } single
            ? new EnchantmentModel[] { single }
            : Array.Empty<EnchantmentModel>();
    }

    /// <summary>取载体实例本身；补丁需要直接读写载体的子附魔时使用。</summary>
    internal static bool TryGetSlot(
        CardModel? card,
        out CompanionEnhancementSlot slot
    )
    {
        if (card?.Enchantment is CompanionEnhancementSlot found)
        {
            slot = found;
            return true;
        }

        slot = null!;
        return false;
    }

    // ---------------------------------------------------------------------
    // 写入：强化槽
    // ---------------------------------------------------------------------

    /// <summary>
    /// 给伴生牌挂载强化。返回实际生效的强化实例（已有同类型强化时就是那一项，层数已叠加），
    /// 目标不是伴生牌或槽内已有其它类型强化时返回 null 且不改动卡牌。
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
        // 「返回 null 且不改动卡牌」，所以必须在进入载体之前拦下来。
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
        if (card.Enchantment is CompanionEnhancementSlot existingSlot &&
            existingSlot.Enhancement is { } current &&
            current.GetType() != enhancement.GetType())
        {
            Entry.Logger.Warn(
                $"[Companion/Enhancement] card={card.Id} action=Reject " +
                $"incoming={enhancement.Id} existing={current.Id} " +
                "reason=SlotHoldsSingleEnhancement"
            );
            return null;
        }

        CompanionEnhancementSlot slot = EnsureSlot(card);
        AbstractCompanionEnhancement attached = slot.AttachEnhancement(
            enhancement,
            Math.Max(1, amount)
        );

        card.DynamicVars.RecalculateForUpgradeOrEnchant();
        card.FinalizeUpgradeInternal();
        return attached;
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
    /// 卸下强化槽内容。卡上仍有原生附魔时，会把它还原成直接挂载的形态并保留其层数；
    /// 返回是否真的卸下了强化（槽为空时返回 false 且不改动卡牌）。
    /// </summary>
    public static bool Remove(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!TryGetSlot(card, out CompanionEnhancementSlot slot))
        {
            return false;
        }

        card.AssertMutable();

        AbstractCompanionEnhancement? enhancement = slot.DetachEnhancement();
        if (enhancement == null)
        {
            return false;
        }

        EnchantmentModel? regular = slot.DetachRegular();

        // 载体本身不再需要，先摘下来；随后把原生附魔重新直接挂回卡上。
        card.ClearEnchantmentInternal();
        enhancement.ClearInternal();

        if (regular != null)
        {
            regular.ClearInternal();
            card.EnchantInternal(regular, regular.Amount);
        }

        card.DynamicVars.RecalculateForUpgradeOrEnchant();
        card.FinalizeUpgradeInternal();
        return true;
    }

    /// <summary>
    /// 只清空原生附魔栏位，保留强化槽内容与载体。
    /// 返回是否真的清掉了原生附魔（本来就没有时返回 false 且不改动卡牌）。
    /// </summary>
    public static bool ClearRegular(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!TryGetSlot(card, out CompanionEnhancementSlot slot))
        {
            return false;
        }

        card.AssertMutable();

        EnchantmentModel? regular = slot.DetachRegular();
        if (regular == null)
        {
            return false;
        }

        // 摘下来的原生附魔不能再保持对同一张卡的绑定，否则卡上会同时存在两个「已绑卡」的附魔。
        regular.ClearInternal();

        card.DynamicVars.RecalculateForUpgradeOrEnchant();
        card.FinalizeUpgradeInternal();
        return true;
    }

    // ---------------------------------------------------------------------
    // 写入：催动蛊牌 → 伴生牌
    // ---------------------------------------------------------------------

    /// <summary>
    /// 给「该蛊牌的伴生牌」挂载强化。这是催动蛊牌产生强化的唯一入口：
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

    // ---------------------------------------------------------------------
    // 内部
    // ---------------------------------------------------------------------

    /// <summary>
    /// 保证卡上存在载体。卡上原本没有附魔时直接让载体占住字段；已有原生附魔时先摘下来
    /// 再交给载体接管 —— 顺序不能反，<see cref="EnchantmentModel.ApplyInternal"/> 要求
    /// 目标附魔当前没有绑定任何卡。
    /// </summary>
    private static CompanionEnhancementSlot EnsureSlot(CardModel card)
    {
        if (card.Enchantment is CompanionEnhancementSlot existing)
        {
            return existing;
        }

        EnchantmentModel? regular = card.Enchantment;
        CompanionEnhancementSlot slot =
            (CompanionEnhancementSlot)
            ModelDb.Enchantment<CompanionEnhancementSlot>().ToMutable();

        card.ClearEnchantmentInternal();
        card.EnchantInternal(slot, 1m);

        if (regular != null)
        {
            slot.ImportExisting(regular);
        }

        return slot;
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
