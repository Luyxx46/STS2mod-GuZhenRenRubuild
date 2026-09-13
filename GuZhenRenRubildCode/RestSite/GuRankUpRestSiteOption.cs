using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Patches;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.RestSite;

/// <summary>
/// 独立篝火“升炼”：每次 2 槽，六转以下消耗 1 槽，六转及以上消耗 2 槽。
/// 普通 Upgrade 不再修改蛊转数。
/// </summary>
public sealed class GuRankUpRestSiteOption : GuRestSiteOptionBase
{
    internal const string OptionIdentifier = "GU_ZHEN_REN_RUBILD_GU_RANK_UP";

    private readonly List<CardModel> _lastLocalVfxCards = [];

    public GuRankUpRestSiteOption(Player owner)
        : base(owner, OptionIdentifier)
    {
    }

    public override bool IsEnabled => HasEligibleCard(Owner);

    protected override IReadOnlyList<CardModel> LocalVfxCards =>
        _lastLocalVfxCards;

    public override async Task<bool> OnSelect()
    {
        _lastLocalVfxCards.Clear();

        if (!IsEnabled)
        {
            return false;
        }

        int remainingSlots = GuRankUpRules.SlotBudget;
        HashSet<CardModel> handledCards = [];
        LocString selectionPrompt = GetOptionText("selectionPrompt");

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

            CardSelectorPrefs prefs = new(selectionPrompt, 1)
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

    private static bool HasEligibleCard(Player player) =>
        player.Deck.Cards.Any(GuRankUpRules.CanRankUp);
}
