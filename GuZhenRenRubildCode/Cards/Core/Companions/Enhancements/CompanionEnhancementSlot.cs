using System.Text.Json;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 「强化槽」的复合附魔载体。
///
/// 原版 <see cref="CardModel"/> 只有一个 <see cref="CardModel.Enchantment"/> 字段，
/// 原生附魔与强化因此无法直接共存。本载体占用该物理字段，内部同时保存两项彼此独立的子附魔：
/// <list type="bullet">
/// <item><see cref="Regular"/>：卡牌原本的原生附魔（怪物附魔、事件附魔、篝火附魔等）。</item>
/// <item><see cref="Enhancement"/>：强化槽内容，即催动蛊牌产生的强化。</item>
/// </list>
///
/// 载体只在「需要共存」时才出现：卡上原本没有附魔时强化直接占字段，只有卡上已有原生附魔
/// 才会转成载体；卸下强化后若仍有原生附魔，会还原成直接挂载的形态。这样原版中所有
/// <c>card.Enchantment is 某附魔</c> 的判定在绝大多数情况下仍然成立。
///
/// 载体自身不显示数量、不追加卡面正文，只负责把两项子附魔的数值加成、战斗钩子、
/// 悬浮提示与状态汇总转发出去；卡面的第二个图标页签由
/// <c>CompanionEnhancementSlotPatch</c> 负责绘制。
/// </summary>
[RegisterEnchantment]
public sealed class CompanionEnhancementSlot : ModEnchantmentTemplate
{
    // 两个子附魔分别保存，互不覆盖。字段只在 service / 本类的内部写入口里改动。
    private EnchantmentModel? _regular;
    private AbstractCompanionEnhancement? _enhancement;

    // 已订阅 StatusChanged 的子附魔。克隆时必须换成新列表，否则克隆体会与原件共享同一个列表。
    private List<EnchantmentModel> _subscribed = [];

    // ---------------------------------------------------------------------
    // 持久化
    // ---------------------------------------------------------------------
    //
    // 存档只认识 AbstractModel 上的 [SavedProperty]，而 EnchantmentModel 本身没有承载
    // 子附魔的位置，因此这里把两项子附魔各序列化成一段 JSON 字符串保存。
    // 使用 SaveIfNotTypeDefault：值为 null（该项不存在）时直接不写，不产生空条目。

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private string? SavedEnhancementJson
    {
        get => Serialize(_enhancement);
        set
        {
            _enhancement = Deserialize<AbstractCompanionEnhancement>(value);
            Amount = 1;
            RefreshStatus();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private string? SavedRegularJson
    {
        get => Serialize(_regular);
        set
        {
            _regular = Deserialize<EnchantmentModel>(value);
            Amount = 1;
            RefreshStatus();
        }
    }

    // ---------------------------------------------------------------------
    // 载体自身的显示语义
    // ---------------------------------------------------------------------

    // 卡面正文由 CompanionEnhancementSlotPatch 追加两项子附魔各自的 extraCardText，
    // 因此载体自己不再产出 extraCardText，避免重复。
    public override bool HasExtraCardText => false;

    // 一个槽只画一个图标（属于强化），数量显示在强化那一格上，载体不显示数量。
    public override bool ShowAmount => false;

    public override int DisplayAmount => 1;

    // 禁止原版把载体本身当作附魔施加到卡上：载体只能由 CompanionEnhancementService 建立。
    public override bool CanEnchant(CardModel card) => false;

    // 载体自身的图标只在「两项子附魔都取不到」这种不应发生的兜底场景下才会被画出来。
    // 两条图标入口都指向同一个占位 PNG：RitsuLib 的附魔图标覆盖既能读 CustomIconPath，
    // 也能读 AssetProfile，同时给出可避免依赖它优先读哪一个。
    public override string? CustomIconPath => SlotIconPath;

    public override EnchantmentAssetProfile AssetProfile => new(
        IconPath: SlotIconPath
    );

    private static string SlotIconPath =>
        $"{Entry.ResPath}/images/enchantments/CompanionEnhancementSlot.png";

    // ---------------------------------------------------------------------
    // 子附魔访问
    // ---------------------------------------------------------------------

    /// <summary>卡牌原本的原生附魔；没有时为 null。只读，改写请走 <see cref="CompanionEnhancementService"/>。</summary>
    internal EnchantmentModel? Regular
    {
        get
        {
            EnsureInnerBindings();
            return _regular;
        }
    }

    /// <summary>强化槽当前内容；为空表示只有原生附魔或两者都空。只读。</summary>
    internal AbstractCompanionEnhancement? Enhancement
    {
        get
        {
            EnsureInnerBindings();
            return _enhancement;
        }
    }

    /// <summary>按「原生附魔 → 强化」的固定顺序返回当前槽内全部子附魔。</summary>
    internal IReadOnlyList<EnchantmentModel> InnerEnchantments
    {
        get
        {
            EnsureInnerBindings();
            return RawInner().ToArray();
        }
    }

    // ---------------------------------------------------------------------
    // 转发给子附魔的钩子
    // ---------------------------------------------------------------------

    public override bool ShouldStartAtBottomOfDrawPile
    {
        get
        {
            EnsureInnerBindings();
            if (HasCard && Card.Keywords.Contains(CardKeyword.Innate))
            {
                return false;
            }

            return RawInner().Any(
                static enchantment => enchantment.ShouldStartAtBottomOfDrawPile
            );
        }
    }

    public override bool ShouldGlowGold
    {
        get
        {
            EnsureInnerBindings();
            return RawInner().Any(
                static enchantment => enchantment.ShouldGlowGold
            );
        }
    }

    public override bool ShouldGlowRed
    {
        get
        {
            EnsureInnerBindings();
            return RawInner().Any(
                static enchantment => enchantment.ShouldGlowRed
            );
        }
    }

    // 原版 HoverTips = 自身提示 + ExtraHoverTips，因此这里把两项子附魔的提示交给原生流程；
    // 补丁只是在需要时把顺序固定成「强化槽 → 原生附魔 → 强化」，不会重复追加。
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            EnsureInnerBindings();
            return RawInner()
                .SelectMany(static enchantment => enchantment.HoverTips)
                .ToList();
        }
    }

    protected override void OnEnchant()
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            enchantment.ModifyCard();
        }

        Amount = 1;
        RefreshStatus();
    }

    public override void RecalculateValues()
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            enchantment.RecalculateValues();
        }

        if (HasCard)
        {
            Card.DynamicVars.RecalculateForUpgradeOrEnchant();
        }

        RefreshStatus();
    }

    // 子附魔必须通过附魔专属的加成钩子影响数值，因此载体把这些钩子按同样的语义串起来：
    // 逐项「先加后乘」，与旧版复合附魔载体一致。

    public override decimal EnchantBlockAdditive(decimal originalBlock)
    {
        EnsureInnerBindings();
        return CalculateFinalBlock(originalBlock) - originalBlock;
    }

    public override decimal EnchantBlockMultiplicative(decimal originalBlock) => 1m;

    public override decimal EnchantDamageAdditive(
        decimal originalDamage,
        ValueProp props
    )
    {
        EnsureInnerBindings();
        return CalculateFinalDamage(originalDamage, props) - originalDamage;
    }

    public override decimal EnchantDamageMultiplicative(
        decimal originalDamage,
        ValueProp props
    ) => 1m;

    public override int EnchantPlayCount(int originalPlayCount)
    {
        EnsureInnerBindings();

        int current = originalPlayCount;
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            current = enchantment.EnchantPlayCount(current);
        }
        return current;
    }

    public override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay? cardPlay
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            await enchantment.OnPlay(choiceContext, cardPlay);
            enchantment.InvokeExecutionFinished();
        }

        RefreshStatus();
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            // 原版有少数附魔会绕过钩子链，在 AfterCardPlayed 里直接对「持久侧附魔实例」
            // 的 Amount 自增（典型是 Goopy：先给战斗实例自增，再对
            // Card.DeckVersion.Enchantment 自增）。这类附魔落在本载体的原生栏位时，
            // DeckVersion.Enchantment 就是载体本身，于是持久侧永远不涨、载体 Amount
            // 反被顶成 2/3/…，破坏「载体 Amount 恒为 1」的不变式。
            //
            // 必须整支跳过转发而不是「转发后再纠正」：转发已经写错了载体 Amount，
            // 而纠正无法把战斗实例的自增补回正确语义。这里改为把这套双写等价地
            // 分别落到「战斗实例」与「同类型的那个内层」上。
            if (enchantment is Goopy goopy)
            {
                if (ReferenceEquals(cardPlay.Card, goopy.Card))
                {
                    // 等价替代 Goopy.AfterCardPlayed 的战斗实例自增。
                    goopy.Amount++;
                    goopy.RecalculateValues();

                    // 等价替代它对 Card.DeckVersion.Enchantment 的自增，
                    // 但目标换成持久侧「同类型的内层」而不是载体。
                    TryIncrementDeckVersionInner(goopy);

                    goopy.Card.DynamicVars.RecalculateForUpgradeOrEnchant();
                }

                enchantment.InvokeExecutionFinished();
                continue;
            }

            await enchantment.AfterCardPlayed(choiceContext, cardPlay);
            enchantment.InvokeExecutionFinished();
        }

        RefreshStatus();
    }

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            await enchantment.AfterCardDrawn(choiceContext, card, fromHandDraw);
        }

        RefreshStatus();
    }

    public override async Task AfterAutoPrePlayPhaseEntered(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            await enchantment.AfterAutoPrePlayPhaseEntered(choiceContext, player);
        }

        RefreshStatus();
    }

    public override async Task BeforeFlush(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            await enchantment.BeforeFlush(choiceContext, player);
        }

        RefreshStatus();
    }

    public override void ModifyShuffleOrder(
        Player player,
        List<CardModel> cards,
        bool isInitialShuffle
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            enchantment.ModifyShuffleOrder(player, cards, isInitialShuffle);
        }
    }

    // ---------------------------------------------------------------------
    // 供 CompanionEnhancementService 使用的写入口
    // ---------------------------------------------------------------------

    /// <summary>把一个已经存在于卡上的原生附魔接管进本载体。调用前本载体必须已经挂在卡上。</summary>
    internal void ImportExisting(EnchantmentModel enchantment)
    {
        ArgumentNullException.ThrowIfNull(enchantment);

        AssertMutable();
        EnsureAttachedToCard();

        if (enchantment.HasCard)
        {
            if (!ReferenceEquals(enchantment.Card, Card))
            {
                enchantment.ClearInternal();
                enchantment.ApplyInternal(Card, enchantment.Amount);
            }
        }
        else
        {
            enchantment.ApplyInternal(Card, enchantment.Amount);
        }

        _regular = enchantment;
        Subscribe(enchantment);
        Amount = 1;
        RefreshStatus();
    }

    /// <summary>写入原生附魔栏位：同类型且可叠加时叠层，否则由调用方保证不会走到这里。</summary>
    internal EnchantmentModel AddOrStackRegular(
        EnchantmentModel enchantment,
        decimal amount
    )
    {
        ArgumentNullException.ThrowIfNull(enchantment);

        AssertMutable();
        EnsureAttachedToCard();
        EnsureInnerBindings();

        if (_regular is { } existing)
        {
            if (existing.GetType() != enchantment.GetType() ||
                !enchantment.IsStackable)
            {
                throw new InvalidOperationException(
                    $"Card {Card.Id} already has regular enchantment " +
                    $"{existing.Id}; cannot add {enchantment.Id}."
                );
            }

            existing.Amount += (int)amount;
            existing.RecalculateValues();
            Card.DynamicVars.RecalculateForUpgradeOrEnchant();
            RefreshStatus();
            return existing;
        }

        enchantment.ApplyInternal(Card, amount);
        _regular = enchantment;
        Subscribe(enchantment);
        Amount = 1;
        enchantment.ModifyCard();
        RefreshStatus();
        return enchantment;
    }

    /// <summary>
    /// 写入强化槽：同类型按层叠加（上限由 <see cref="AbstractCompanionEnhancement.MaxAmount"/> 决定），
    /// 不同类型由调用方拦截；这里只做兜底拒绝，避免一个槽被写成多项。
    /// </summary>
    internal AbstractCompanionEnhancement AttachEnhancement(
        AbstractCompanionEnhancement enhancement,
        decimal amount
    )
    {
        ArgumentNullException.ThrowIfNull(enhancement);

        AssertMutable();
        EnsureAttachedToCard();
        EnsureInnerBindings();

        if (_enhancement is { } existing)
        {
            if (existing.GetType() != enhancement.GetType())
            {
                throw new InvalidOperationException(
                    $"Card {Card.Id} already has enhancement {existing.Id}; " +
                    $"cannot add {enhancement.Id}. The slot holds a single enhancement."
                );
            }

            existing.Amount = Math.Min(
                existing.MaxAmount,
                existing.Amount + (int)amount
            );
            existing.RecalculateValues();
            Card.DynamicVars.RecalculateForUpgradeOrEnchant();
            RefreshStatus();
            return existing;
        }

        enhancement.ApplyInternal(Card, amount);
        _enhancement = enhancement;
        Subscribe(enhancement);
        Amount = 1;
        enhancement.ModifyCard();
        RefreshStatus();
        return enhancement;
    }

    /// <summary>摘出原生附魔，但不清除它自身的卡绑定（由调用方决定后续去向）。</summary>
    internal EnchantmentModel? DetachRegular()
    {
        EnchantmentModel? regular = _regular;
        if (regular == null)
        {
            return null;
        }

        Unsubscribe(regular);
        _regular = null;
        Amount = 1;
        RefreshStatus();
        return regular;
    }

    /// <summary>摘出强化槽内容，但不清除它自身的卡绑定（由调用方决定后续去向）。</summary>
    internal AbstractCompanionEnhancement? DetachEnhancement()
    {
        AbstractCompanionEnhancement? enhancement = _enhancement;
        if (enhancement == null)
        {
            return null;
        }

        Unsubscribe(enhancement);
        _enhancement = null;
        Amount = 1;
        RefreshStatus();
        return enhancement;
    }

    /// <summary>把载体状态汇总成子附魔的状态：任一子附魔可用则载体可用。</summary>
    internal void RefreshStatus()
    {
        Status = RawInner().Any(
            static enchantment => enchantment.Status == EnchantmentStatus.Normal
        )
            ? EnchantmentStatus.Normal
            : EnchantmentStatus.Disabled;
    }

    // ---------------------------------------------------------------------
    // 克隆与内部绑定
    // ---------------------------------------------------------------------

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();

        _regular = (EnchantmentModel?)_regular?.ClonePreservingMutability();
        _enhancement =
            (AbstractCompanionEnhancement?)
            _enhancement?.ClonePreservingMutability();

        // MemberwiseClone 会共享同一个列表实例，必须换成新列表，否则清理克隆体时会把原件的订阅一起清掉。
        // 内层附魔的 StatusChanged 已在 EnchantmentModel.DeepCloneFields 里置空，因此这里不需要退订。
        _subscribed = [];
    }

    /// <summary>
    /// 反序列化与克隆都只会恢复字段，不会恢复「子附魔指向哪张卡」。这里把断开的绑定补回去，
    /// 并把还没订阅过的子附魔接上状态变更事件。
    /// </summary>
    private void EnsureInnerBindings()
    {
        if (!HasCard)
        {
            return;
        }

        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            if (!enchantment.HasCard || !ReferenceEquals(enchantment.Card, Card))
            {
                if (enchantment.HasCard)
                {
                    // ApplyInternal 在 Card != null 时会抛异常，必须先断开旧绑定。
                    enchantment.ClearInternal();
                }

                enchantment.ApplyInternal(Card, enchantment.Amount);
            }

            Subscribe(enchantment);
        }
    }

    // 只读遍历两个字段，绝不触发 EnsureInnerBindings，避免与绑定流程互相递归。
    private IEnumerable<EnchantmentModel> RawInner()
    {
        if (_regular != null)
        {
            yield return _regular;
        }

        if (_enhancement != null)
        {
            yield return _enhancement;
        }
    }

    private decimal CalculateFinalBlock(decimal originalBlock)
    {
        decimal current = originalBlock;
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            current += enchantment.EnchantBlockAdditive(current);
            current *= enchantment.EnchantBlockMultiplicative(current);
        }
        return current;
    }

    private decimal CalculateFinalDamage(decimal originalDamage, ValueProp props)
    {
        decimal current = originalDamage;
        foreach (EnchantmentModel enchantment in RawInner().ToArray())
        {
            current += enchantment.EnchantDamageAdditive(current, props);
            current *= enchantment.EnchantDamageMultiplicative(current, props);
        }
        return current;
    }

    /// <summary>
    /// 「原版会直写 <c>Card.DeckVersion.Enchantment.Amount</c> 的附魔」的通用兼容通道。
    ///
    /// 这类附魔（典型是 <see cref="Goopy"/>）绕过钩子链，直接对持久侧的附魔实例自增层数。
    /// 当它落在本载体的栏位里时，那个对象是载体本身，自增会写坏载体的 Amount。
    /// 本助手按运行时类型解析出持久侧「同类型的那个内层」并自增，不硬编码任何具体
    /// 附魔类型或载体形态；解析不到就静默返回，绝不抛异常。
    /// </summary>
    private void TryIncrementDeckVersionInner(EnchantmentModel inner)
    {
        EnchantmentModel? deckEnchantment = inner.Card?.DeckVersion?.Enchantment;
        if (deckEnchantment == null)
        {
            return;
        }

        if (deckEnchantment is CompanionEnhancementSlot deckSlot)
        {
            EnchantmentModel? regular = deckSlot.Regular;
            AbstractCompanionEnhancement? enhancement = deckSlot.Enhancement;

            // 顺序与 RawInner() 一致：先原生栏位，再强化栏位；只认类型相同的那一项。
            EnchantmentModel? target = null;
            if (regular != null && regular.GetType() == inner.GetType())
            {
                target = regular;
            }
            else if (enhancement != null &&
                enhancement.GetType() == inner.GetType())
            {
                target = enhancement;
            }

            if (target == null)
            {
                return;
            }

            target.Amount++;
            target.RecalculateValues();
            deckSlot.RefreshStatus();
            return;
        }

        // 持久侧不是载体（卡上没有载体，或附魔直接挂在卡上）时，按原版语义直接自增。
        if (deckEnchantment.GetType() == inner.GetType())
        {
            deckEnchantment.Amount++;
            deckEnchantment.RecalculateValues();
        }
    }

    private void EnsureAttachedToCard()
    {
        if (!HasCard)
        {
            throw new InvalidOperationException(
                "Companion enhancement slot must be attached to a card first."
            );
        }
    }

    private void Subscribe(EnchantmentModel enchantment)
    {
        if (_subscribed.Contains(enchantment))
        {
            return;
        }

        enchantment.StatusChanged += OnInnerStatusChanged;
        _subscribed.Add(enchantment);
    }

    private void Unsubscribe(EnchantmentModel enchantment)
    {
        enchantment.StatusChanged -= OnInnerStatusChanged;
        _subscribed.Remove(enchantment);
    }

    private void OnInnerStatusChanged() => RefreshStatus();

    private static string? Serialize(EnchantmentModel? enchantment) =>
        enchantment == null
            ? null
            : JsonSerializer.Serialize(enchantment.ToSerializable());

    private static TEnchantment? Deserialize<TEnchantment>(string? json)
        where TEnchantment : EnchantmentModel
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        SerializableEnchantment? serialized =
            JsonSerializer.Deserialize<SerializableEnchantment>(json);
        return serialized == null
            ? null
            : EnchantmentModel.FromSerializable(serialized) as TEnchantment;
    }
}
