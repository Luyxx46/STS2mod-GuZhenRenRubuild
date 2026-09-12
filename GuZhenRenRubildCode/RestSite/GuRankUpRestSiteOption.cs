using Godot;
using GuZhenRenRubild.Cards;
using GuZhenRenRubild.Cards.Rules;
using GuZhenRenRubild.Patches;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.RestSite;

/// <summary>
/// 独立篝火“升炼”：每次 2 槽，六转以下消耗 1 槽，六转及以上消耗 2 槽。
/// 普通 Upgrade 不再修改蛊转数。
/// </summary>
public sealed class GuRankUpRestSiteOption : ModRestSiteOptionTemplate
{
    internal const string OptionIdentifier = "GU_ZHEN_REN_RUBILD_GU_RANK_UP";

    private static readonly LocString DescriptionText = new(
        "rest_site_ui",
        "OPTION_GU_ZHEN_REN_RUBILD_GU_RANK_UP.description"
    );
    private static readonly LocString SelectionPrompt = new(
        "rest_site_ui",
        "OPTION_GU_ZHEN_REN_RUBILD_GU_RANK_UP.selectionPrompt"
    );
    private const string FallbackIconPath = "res://images/ui/rest_site/option_smith.png";

    private readonly List<CardModel> _lastLocalVfxCards = [];

    public GuRankUpRestSiteOption(Player owner) : base(owner)
    {
    }

    public override string OptionId => OptionIdentifier;
    public override LocString Description => DescriptionText;
    public override bool IsEnabled => HasEligibleCard(Owner);
    public override RestSiteOptionAssetProfile AssetProfile =>
        new(IconPath: FallbackIconPath);
    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(NCardSmithVfx.AssetPaths);

    public override async Task<bool> OnSelect()
    {
        _lastLocalVfxCards.Clear();
        if (!IsEnabled)
        {
            return false;
        }

        int remainingSlots = GuRankUpRules.SlotBudget;
        HashSet<CardModel> handledCards = [];

        while (remainingSlots > 0)
        {
            bool hasCandidate = Owner.Deck.Cards.Any(card =>
                !handledCards.Contains(card) &&
                GuRankUpRules.CanRankUp(card) &&
                GuRankUpRules.GetSlotCost(card) <= remainingSlots
            );
            if (!hasCandidate)
            {
                break;
            }

            CardSelectorPrefs prefs = new(SelectionPrompt, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };

            CardModel? selected;
            using (GuRankUpPreviewPatch.Begin(remainingSlots, handledCards))
            {
                selected = (await CardSelectCmd.FromDeckForUpgrade(Owner, prefs))
                    .FirstOrDefault();
            }

            if (selected is not AbstractGuCard gu)
            {
                break;
            }

            handledCards.Add(gu);
            int slotCost = GuRankUpRules.GetSlotCost(gu);
            if (slotCost > remainingSlots || !gu.TryIncreaseGuRank())
            {
                continue;
            }

            remainingSlots -= slotCost;
            _lastLocalVfxCards.Add(gu);
        }

        return _lastLocalVfxCards.Count > 0;
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        if (_lastLocalVfxCards.Count == 0)
        {
            return;
        }

        NCardSmithVfx? vfx = NCardSmithVfx.Create(_lastLocalVfxCards);
        if (vfx == null)
        {
            return;
        }

        NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        await Cmd.CustomScaledWait(1f, 2f, ignoreCombatEnd: false, ct);
    }

    public override Task DoRemotePostSelectVfx()
    {
        NRestSiteCharacter? characterNode = NRestSiteRoom.Instance?
            .Characters
            .FirstOrDefault(character => character.Player == Owner);
        NCardSmithVfx? vfx = NCardSmithVfx.Create();
        if (characterNode == null || vfx == null)
        {
            return Task.CompletedTask;
        }

        characterNode.AddChildSafely(vfx);
        vfx.Position = Vector2.Zero;
        return Task.CompletedTask;
    }

    private static bool HasEligibleCard(Player player) =>
        player.Deck.Cards.Any(GuRankUpRules.CanRankUp);
}
