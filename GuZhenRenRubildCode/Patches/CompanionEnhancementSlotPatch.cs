using System.Reflection;

using Godot;

using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs.History;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 让「强化槽」在卡牌界面上表现为独立于原生附魔的第二个附魔栏位。
///
/// 强化槽由 <see cref="CompanionEnhancementSlot"/> 占用原版唯一的 <c>CardModel.Enchantment</c>
/// 字段承载，因此原版所有「读写卡牌附魔」的入口都必须知道载体的存在：
/// 附魔能否施加、施加到哪里、怎么清空、卡面画几个图标、悬浮提示与卡面正文追加什么、
/// 附魔预览与附魔特效显示哪一项。
///
/// 本补丁只在卡牌真的带载体时接管，其余卡牌（含只带普通原生附魔的卡）一律原样放行原版逻辑。
/// </summary>
internal static class CompanionEnhancementSlotPatch
{
    private const string HarmonyId = Entry.ModId + ".CompanionEnhancementSlot";

    // 复制出来的第二个页签统一用该前缀命名，清理时按前缀匹配。
    private const string ExtraTabPrefix = "GuZhenRenRubildEnhancementTab";

    // 页签染色的 shader 参数名，与原版 NCard 使用同一组参数。
    private static readonly StringName TintHue = new("h");
    private static readonly StringName TintSaturation = new("s");
    private static readonly StringName TintValue = new("v");

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    // 反射到的私有成员。全部在 Initialize 的 configure 里解析，失败时由 Entry 的事务式回滚接管。
    private static FieldInfo _nCardIcon = null!;
    private static FieldInfo _nCardLabel = null!;
    private static FieldInfo _nCardDefaultEnchantmentPosition = null!;
    private static FieldInfo _enchantPreviewBefore = null!;
    private static FieldInfo _enchantPreviewAfter = null!;
    private static MethodInfo _enchantPreviewRemoveExistingCards = null!;
    private static FieldInfo _enchantVfxCardNode = null!;
    private static FieldInfo _enchantVfxIcon = null!;
    private static FieldInfo _enchantVfxLabel = null!;
    private static FieldInfo _enchantVfxCardModel = null!;

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            ResolvePrivateMembers();

            harmony.Patch(
                RequiredMember.Method(
                    typeof(EnchantmentModel),
                    nameof(EnchantmentModel.CanEnchant),
                    [typeof(CardModel)]
                ),
                prefix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(CanEnchantPrefix)
                )
            );
            harmony.Patch(
                RequiredMember.Method(
                    typeof(CardCmd),
                    nameof(CardCmd.Enchant),
                    [typeof(EnchantmentModel), typeof(CardModel), typeof(decimal)]
                ),
                prefix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(EnchantPrefix)
                )
            );
            harmony.Patch(
                RequiredMember.Method(
                    typeof(CardCmd),
                    nameof(CardCmd.ClearEnchantment),
                    [typeof(CardModel)]
                ),
                prefix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(ClearEnchantmentPrefix)
                )
            );
            harmony.Patch(
                RequiredMember.PropertyGetter(
                    typeof(EnchantmentModel),
                    nameof(EnchantmentModel.HoverTips)
                ),
                postfix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(HoverTipsPostfix)
                )
            );
            harmony.Patch(
                RequiredMember.Method(
                    typeof(CardModel),
                    nameof(CardModel.GetDescriptionForPile),
                    [typeof(PileType), typeof(Creature)]
                ),
                postfix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(DescriptionPostfix)
                )
            );
            harmony.Patch(
                RequiredMember.Method(
                    typeof(CardModel),
                    nameof(CardModel.GetDescriptionForUpgradePreview),
                    Type.EmptyTypes
                ),
                postfix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(DescriptionPostfix)
                )
            );
            harmony.Patch(
                RequiredMember.DeclaredMethod(
                    typeof(NCard),
                    "UpdateEnchantmentVisuals",
                    Type.EmptyTypes
                ),
                prefix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(UpdateEnchantmentVisualsPrefix)
                )
            );
            harmony.Patch(
                RequiredMember.DeclaredMethod(
                    typeof(NEnchantPreview),
                    nameof(NEnchantPreview.Init),
                    [typeof(CardModel), typeof(EnchantmentModel), typeof(int)]
                ),
                prefix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(EnchantPreviewPrefix)
                )
            );
            harmony.Patch(
                RequiredMember.DeclaredMethod(
                    typeof(NCardEnchantVfx),
                    nameof(NCardEnchantVfx._Ready),
                    Type.EmptyTypes
                ),
                postfix: new HarmonyMethod(
                    typeof(CompanionEnhancementSlotPatch),
                    nameof(EnchantVfxReadyPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    // ---------------------------------------------------------------------
    // 附魔施加与清空
    // ---------------------------------------------------------------------

    /// <summary>
    /// 带载体的卡上，原版判定看的是载体而不是真正的原生附魔栏位。
    /// 这里把判定换成「按原生附魔栏位判断」，载体本身与强化永远不可被原版附魔。
    /// </summary>
    private static bool CanEnchantPrefix(
        EnchantmentModel __instance,
        CardModel card,
        ref bool __result
    )
    {
        if (!CompanionEnhancementService.HasSlot(card))
        {
            return true;
        }

        if (__instance is CompanionEnhancementSlot or
            AbstractCompanionEnhancement)
        {
            __result = false;
            return false;
        }

        if (card.Type is CardType.Status or CardType.Curse or CardType.Quest ||
            !__instance.CanEnchantCardType(card.Type) ||
            (card.Pile?.Type == PileType.Deck &&
             card.Keywords.Contains(CardKeyword.Unplayable)))
        {
            __result = false;
            return false;
        }

        EnchantmentModel? regular = CompanionEnhancementService.TryGetRegular(
            card
        );
        __result = regular == null ||
            (regular.GetType() == __instance.GetType() &&
             __instance.IsStackable);
        return false;
    }

    /// <summary>
    /// 原版 <c>CardCmd.Enchant</c> 会把附魔写进 <c>card.Enchantment</c>，也就是覆盖掉载体。
    /// 这里改成写进载体的原生附魔栏位，强化不受影响。
    /// </summary>
    private static bool EnchantPrefix(
        EnchantmentModel enchantment,
        CardModel card,
        decimal amount,
        ref EnchantmentModel? __result
    )
    {
        if (!CompanionEnhancementService.TryGetSlot(
                card,
                out CompanionEnhancementSlot slot
            ) ||
            enchantment is CompanionEnhancementSlot or
                AbstractCompanionEnhancement)
        {
            return true;
        }

        enchantment.AssertMutable();
        if (!enchantment.CanEnchant(card))
        {
            throw new InvalidOperationException(
                $"Cannot enchant {card.Id} with {enchantment.Id}."
            );
        }

        __result = slot.AddOrStackRegular(enchantment, amount);
        card.FinalizeUpgradeInternal();
        RecordEnchantmentHistory(card, enchantment.Id);
        return false;
    }

    /// <summary>
    /// 清空附魔时只清原生附魔栏位，强化槽必须保留。
    /// </summary>
    private static bool ClearEnchantmentPrefix(CardModel card)
    {
        if (!CompanionEnhancementService.TryGetSlot(card, out _))
        {
            return true;
        }

        CompanionEnhancementService.ClearRegular(card);
        return false;
    }

    // ---------------------------------------------------------------------
    // 悬浮提示与卡面正文
    // ---------------------------------------------------------------------

    /// <summary>
    /// 载体自身也有一条说明（「强化槽」是什么），其后依次是原生附魔与强化的提示。
    /// 这里整体替换而不是追加，既保证顺序固定，也不会让内层提示出现两次。
    /// </summary>
    private static void HoverTipsPostfix(
        EnchantmentModel __instance,
        ref IEnumerable<IHoverTip> __result
    )
    {
        if (__instance is not CompanionEnhancementSlot slot)
        {
            return;
        }

        List<IHoverTip> tips = [slot.HoverTip];
        foreach (EnchantmentModel inner in slot.InnerEnchantments)
        {
            tips.AddRange(inner.HoverTips);
        }

        __result = tips;
    }

    /// <summary>
    /// 原版只会追加 <c>card.Enchantment.DynamicExtraCardText</c>，也就是载体自己的（载体不产出正文）。
    /// 这里把两项子附魔各自的正文按原版同样的紫色包裹追加回去。
    /// </summary>
    private static void DescriptionPostfix(
        CardModel __instance,
        ref string __result
    )
    {
        if (__instance.Enchantment is not CompanionEnhancementSlot slot)
        {
            return;
        }

        if (slot.Regular?.DynamicExtraCardText is { } regularText)
        {
            AppendDescriptionLine(ref __result, regularText.GetFormattedText());
        }

        if (slot.Enhancement?.DynamicExtraCardText is { } enhancementText)
        {
            AppendDescriptionLine(
                ref __result,
                enhancementText.GetFormattedText()
            );
        }
    }

    private static void AppendDescriptionLine(
        ref string description,
        string? text
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string line = $"[purple]{text}[/purple]";
        if (description.EndsWith(line, StringComparison.Ordinal))
        {
            return;
        }

        description = string.IsNullOrWhiteSpace(description)
            ? line
            : $"{description}\n{line}";
    }

    // ---------------------------------------------------------------------
    // 卡面图标
    // ---------------------------------------------------------------------

    /// <summary>
    /// 载体需要两个图标页签。这里完全接管绘制：先清掉上一次复制出来的页签，
    /// 再把原生附魔与强化各画一格；两者只有一项时行为与原版单页签一致。
    /// </summary>
    private static bool UpdateEnchantmentVisualsPrefix(NCard __instance)
    {
        ClearExtraTabs(__instance);

        if (__instance.Model?.Enchantment is not
            CompanionEnhancementSlot slot)
        {
            return true;
        }

        EnchantmentModel? regular = slot.Regular;
        AbstractCompanionEnhancement? enhancement = slot.Enhancement;
        EnchantmentModel? lead = regular ?? enhancement;

        Control tab = __instance.EnchantmentTab;
        if (lead == null)
        {
            tab.Visible = false;
            return false;
        }

        TextureRect icon = (TextureRect)_nCardIcon.GetValue(__instance)!;
        MegaLabel label = (MegaLabel)_nCardLabel.GetValue(__instance)!;
        Vector2 defaultPosition =
            (Vector2)_nCardDefaultEnchantmentPosition.GetValue(__instance)!;
        Vector2 basePosition = __instance.Model.HasStarCostX ||
            __instance.Model.CurrentStarCost >= 0
                ? defaultPosition
                : defaultPosition + Vector2.Up * 45f;

        tab.Position = basePosition;
        ConfigureTab(tab, icon, label, lead);

        if (regular != null && enhancement != null)
        {
            float spacing = MathF.Max(
                54f,
                (tab.Size.Y > 0f ? tab.Size.Y : 46f) + 6f
            );
            CreateExtraTab(
                __instance,
                tab,
                basePosition + Vector2.Down * spacing,
                enhancement
            );
        }

        return false;
    }

    private static void CreateExtraTab(
        NCard cardNode,
        Control sourceTab,
        Vector2 position,
        EnchantmentModel enchantment
    )
    {
        if (sourceTab.GetParent() is not Node parent ||
            sourceTab.Duplicate() is not Control duplicate)
        {
            return;
        }

        duplicate.Name = ExtraTabPrefix;
        duplicate.Material = duplicate.Material?.Duplicate() as Material;
        duplicate.Position = position;
        parent.AddChildSafely(duplicate);

        // Godot 在 add_child 时为重名节点自动改名，改名结果可能不再带前缀。
        // 清理是按前缀匹配的，所以这里补一次赋值，保证第二个页签始终可被识别。
        if (!duplicate.Name.ToString().StartsWith(
                ExtraTabPrefix,
                StringComparison.Ordinal
            ))
        {
            duplicate.Name = ExtraTabPrefix;
        }

        TextureRect? icon =
            duplicate.GetNodeOrNull<TextureRect>("Icon") ??
            duplicate.FindChild("Icon", true, false) as TextureRect;
        MegaLabel? label =
            duplicate.GetNodeOrNull<MegaLabel>("Label") ??
            duplicate.FindChild("Label", true, false) as MegaLabel;
        if (icon == null || label == null)
        {
            parent.RemoveChildSafely(duplicate);
            duplicate.QueueFreeSafely();
            return;
        }

        ConfigureTab(duplicate, icon, label, enchantment);
    }

    private static void ClearExtraTabs(NCard cardNode)
    {
        Control tab = cardNode.EnchantmentTab;
        if (tab == null)
        {
            return;
        }

        Node? parent = tab.GetParent();
        if (parent == null)
        {
            return;
        }

        foreach (Node child in parent.GetChildren())
        {
            if (!child.Name.ToString().StartsWith(
                    ExtraTabPrefix,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            parent.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }
    }

    private static void ConfigureTab(
        Control tab,
        TextureRect icon,
        MegaLabel label,
        EnchantmentModel enchantment
    )
    {
        tab.Visible = true;
        icon.Texture = enchantment.Icon;
        label.SetTextAutoSize(enchantment.DisplayAmount.ToString());
        label.Visible = enchantment.ShowAmount;
        ApplyStatus(tab, icon, label, enchantment.Status);
    }

    private static void ApplyStatus(
        Control tab,
        TextureRect icon,
        MegaLabel label,
        EnchantmentStatus status
    )
    {
        bool disabled = status == EnchantmentStatus.Disabled;
        tab.Modulate = disabled
            ? new Color(1f, 1f, 1f, 0.9f)
            : Colors.White;

        if (tab.Material is ShaderMaterial material)
        {
            material.SetShaderParameter(TintHue, 0.25);
            material.SetShaderParameter(TintSaturation, disabled ? 0.1 : 0.4);
            material.SetShaderParameter(TintValue, 0.6);
        }

        icon.UseParentMaterial = disabled;
        label.SelfModulate = disabled ? StsColors.gray : Colors.White;
    }

    // ---------------------------------------------------------------------
    // 附魔预览与附魔特效
    // ---------------------------------------------------------------------

    /// <summary>
    /// 原版 <c>NEnchantPreview.Init</c> 直接 <c>EnchantInternal</c>，会把克隆卡上的整个载体换掉，
    /// 导致预览里「附魔后」只剩新附魔、强化消失。这里改成只替换载体的原生附魔栏位。
    /// </summary>
    private static bool EnchantPreviewPrefix(
        NEnchantPreview __instance,
        CardModel card,
        EnchantmentModel canonicalEnchantment,
        int amount
    )
    {
        if (!CompanionEnhancementService.TryGetSlot(card, out _) ||
            canonicalEnchantment is CompanionEnhancementSlot or
                AbstractCompanionEnhancement)
        {
            return true;
        }

        canonicalEnchantment.AssertCanonical();

        // 先克隆并确认克隆体确实带载体，再改动 __instance 的预览节点：万一取不到载体
        // （克隆路径变化等）而要整体退回原版，屏幕上也不会先闪出一张多余的「附魔前」卡
        // —— 原版 Init 自己会重新清理并铺两张卡。
        CardModel previewCard = card.CardScope!.CloneCard(card);
        if (!CompanionEnhancementService.TryGetSlot(
                previewCard,
                out CompanionEnhancementSlot previewSlot
            ))
        {
            return true;
        }

        previewCard.IsEnchantmentPreview = true;

        _enchantPreviewRemoveExistingCards.Invoke(__instance, null);

        Control before = (Control)_enchantPreviewBefore.GetValue(__instance)!;
        Control after = (Control)_enchantPreviewAfter.GetValue(__instance)!;

        NCard beforeCard = NCard.Create(card) ??
            throw new InvalidOperationException(
                "Failed to create before-enchantment preview card."
            );
        NPreviewCardHolder beforeHolder = NPreviewCardHolder.Create(
            beforeCard,
            showHoverTips: true,
            scaleOnHover: false
        ) ?? throw new InvalidOperationException(
            "Failed to create before-enchantment preview holder."
        );
        before.AddChildSafely(beforeHolder);
        beforeHolder.CardNode!.UpdateVisuals(
            card.Pile?.Type ?? PileType.None,
            CardPreviewMode.Normal
        );

        // 预览与原版一致采用「覆盖」语义：先摘掉克隆体上原有的原生附魔，避免
        // 因类型不同或不可叠加而在预览路径抛异常；强化槽内容保持不动。
        previewSlot.DetachRegular()?.ClearInternal();
        previewSlot.AddOrStackRegular(
            canonicalEnchantment.ToMutable(),
            amount
        );
        previewCard.FinalizeUpgradeInternal();

        NCard afterCard = NCard.Create(previewCard) ??
            throw new InvalidOperationException(
                "Failed to create after-enchantment preview card."
            );
        NPreviewCardHolder afterHolder = NPreviewCardHolder.Create(
            afterCard,
            showHoverTips: true,
            scaleOnHover: false
        ) ?? throw new InvalidOperationException(
            "Failed to create after-enchantment preview holder."
        );
        after.AddChildSafely(afterHolder);
        afterHolder.CardNode!.UpdateVisuals(
            PileType.None,
            CardPreviewMode.Normal
        );
        return false;
    }

    /// <summary>
    /// 附魔特效显示的是载体自己的图标，需要改回「原生附魔优先、否则强化」的那一项。
    /// </summary>
    private static void EnchantVfxReadyPostfix(NCardEnchantVfx __instance)
    {
        CardModel card =
            (CardModel)_enchantVfxCardModel.GetValue(__instance)!;
        if (card.Enchantment is not CompanionEnhancementSlot slot)
        {
            return;
        }

        EnchantmentModel? displayed = slot.Regular ?? slot.Enhancement;
        if (displayed == null)
        {
            return;
        }

        NCard cardNode = (NCard)_enchantVfxCardNode.GetValue(__instance)!;
        ClearExtraTabs(cardNode);

        TextureRect icon = (TextureRect)_enchantVfxIcon.GetValue(__instance)!;
        MegaLabel label = (MegaLabel)_enchantVfxLabel.GetValue(__instance)!;
        icon.Texture = displayed.Icon;
        label.SetTextAutoSize(displayed.DisplayAmount.ToString());
        label.Visible = displayed.ShowAmount;
    }

    // ---------------------------------------------------------------------
    // 内部
    // ---------------------------------------------------------------------

    private static void RecordEnchantmentHistory(
        CardModel card,
        ModelId enchantmentId
    )
    {
        if (card.Pile?.Type == PileType.Deck)
        {
            card.Owner.RunState.CurrentMapPointHistoryEntry?
                .GetEntry(card.Owner.NetId)
                .CardsEnchanted.Add(
                    new CardEnchantmentHistoryEntry(card, enchantmentId)
                );
        }
    }

    private static void ResolvePrivateMembers()
    {
        _nCardIcon = RequiredMember.Field(typeof(NCard), "_enchantmentIcon");
        _nCardLabel = RequiredMember.Field(typeof(NCard), "_enchantmentLabel");
        _nCardDefaultEnchantmentPosition = RequiredMember.Field(
            typeof(NCard),
            "_defaultEnchantmentPosition"
        );
        _enchantPreviewBefore = RequiredMember.Field(
            typeof(NEnchantPreview),
            "_before"
        );
        _enchantPreviewAfter = RequiredMember.Field(
            typeof(NEnchantPreview),
            "_after"
        );
        _enchantPreviewRemoveExistingCards = RequiredMember.DeclaredMethod(
            typeof(NEnchantPreview),
            "RemoveExistingCards",
            Type.EmptyTypes
        );
        _enchantVfxCardNode = RequiredMember.Field(
            typeof(NCardEnchantVfx),
            "_cardNode"
        );
        _enchantVfxIcon = RequiredMember.Field(
            typeof(NCardEnchantVfx),
            "_enchantmentIcon"
        );
        _enchantVfxLabel = RequiredMember.Field(
            typeof(NCardEnchantVfx),
            "_enchantmentLabel"
        );
        _enchantVfxCardModel = RequiredMember.Field(
            typeof(NCardEnchantVfx),
            "_cardModel"
        );
    }
}
