using System.Globalization;

using GuZhenRenRubild.Cards.Core;
using GuZhenRenRubild.Cards.Core.Recipes;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.RestSite;

/// <summary>
/// 篝火选项「合练」：玩家先选择一张当前可合成的蛊牌，再选择与该结果匹配的
/// 材料。材料数量由目标牌配方决定，不要求玩家记忆配方。
///
/// 与「升炼」同属休息点选项，遵循游戏原生规则——每个休息点只能执行一次行动。
/// </summary>
public sealed class GuHeLianRestSiteOption : GuRestSiteOptionBase
{
    internal const string OptionIdentifier = "GU_ZHEN_REN_RUBILD_HE_LIAN";

    private const int MinimumSelectableMaterialCount = 1;

    private static readonly string HeLianIconPath =
        $"{Entry.ResPath}/images/rest_site_options/GuZhenRenRubildHeLian.png";

    private CardModel? _lastLocalVfxCard;

    public GuHeLianRestSiteOption(Player owner)
        : base(owner, OptionIdentifier, HeLianIconPath)
    {
    }

    public override bool IsEnabled => HasCraftableRecipe(Owner);

    protected override IReadOnlyList<CardModel> LocalVfxCards =>
        _lastLocalVfxCard == null
            ? Array.Empty<CardModel>()
            : [_lastLocalVfxCard];

    public override async Task<bool> OnSelect()
    {
        _lastLocalVfxCard = null;

        if (!IsEnabled)
        {
            return false;
        }

        return await TryPerformHeLianOnce();
    }

    /// <summary>
    /// 执行单次合练。先选择目标结果牌，再选择该目标的材料。
    /// </summary>
    private async Task<bool> TryPerformHeLianOnce()
    {
        CardModel[] availableMaterials = GetAvailableMaterials(Owner);

        Type[] craftableResultTypes = HeLianRecipeRegistry
            .GetCraftableResultTypes(availableMaterials)
            .ToArray();

        if (craftableResultTypes.Length == 0)
        {
            ShowLocalFeedback(success: false, "feedback.noRecipe");
            return false;
        }

        // 预览牌需要明确的 Owner：休息点不初始化战斗牌堆，显式归属可避免
        // 界面实现或调用时机变化后出现无 Owner 的预览模型。
        CardModel[] targetPreviews = craftableResultTypes
            .Select(resultType =>
                GuCardCatalog.CreateOwnedPreview(resultType, Owner)
            )
            .ToArray();

        CardModel? target = (
            await CardSelectCmd.FromSimpleGrid(
                new BlockingPlayerChoiceContext(),
                targetPreviews,
                Owner,
                new CardSelectorPrefs(
                    GetOptionText("targetSelectionPrompt"),
                    1
                )
                {
                    Cancelable = true,
                    PretendCardsCanBePlayed = true,
                }
            )
        ).FirstOrDefault();

        if (target == null)
        {
            return false;
        }

        Type targetCardType = target.GetType();
        IReadOnlyList<Type> materialTypes =
            HeLianRecipeRegistry.GetMaterialTypesForResult(
                targetCardType,
                out _
            );

        if (!HeLianRecipeRegistry.GetMaterialCountRangeForResult(
                targetCardType,
                out int minimumMaterialCount,
                out int maximumMaterialCount
            ))
        {
            return false;
        }

        CardSelectorPrefs prefs = new(
            GetOptionText("selectionPrompt"),
            Math.Max(MinimumSelectableMaterialCount, minimumMaterialCount),
            maximumMaterialCount
        )
        {
            Cancelable = true,
            // 材料数量本身就是配方的一部分：上下限一致时无需再逐张确认。
            RequireManualConfirmation =
                minimumMaterialCount != maximumMaterialCount,
            PretendCardsCanBePlayed = true,
        };

        List<CardModel> selectedCards = (
            await CardSelectCmd.FromDeckGeneric(
                player: Owner,
                prefs: prefs,
                filter: card =>
                    IsEligibleMaterial(card) &&
                    materialTypes.Contains(card.GetType()) &&
                    HeLianRecipeRegistry.IsEligibleMaterialCardForResult(
                        card,
                        targetCardType
                    ),
                sortingOrder: GetMaterialSortOrder
            )
        ).ToList();

        if (selectedCards.Count < minimumMaterialCount ||
            selectedCards.Count > maximumMaterialCount)
        {
            return false;
        }

        if (!HeLianRecipeRegistry.TryCreateResultForTarget(
                selectedCards,
                Owner,
                targetCardType,
                out AbstractGuCard? result
            ))
        {
            string selectedMaterialNames = string.Join(
                " + ",
                selectedCards.Select(card => card.Title)
            );

            Entry.Logger.Info(
                "合练失败：所选材料未匹配任何合练配方。" +
                $" 材料={selectedMaterialNames}"
            );

            ShowLocalFeedback(
                success: false,
                "feedback.unmatchedMaterials",
                ("Materials", selectedMaterialNames)
            );
            return false;
        }

        List<MaterialSnapshot> materialSnapshots = selectedCards
            .Select(card => new MaterialSnapshot(
                card,
                Owner.Deck.Cards.ToList().IndexOf(card)
            ))
            .OrderBy(snapshot => snapshot.DeckIndex)
            .ToList();

        var playerHistory = Owner.RunState
            .CurrentMapPointHistoryEntry?
            .GetEntry(Owner.NetId);

        int removedHistoryCount = playerHistory?.CardsRemoved.Count ?? 0;
        int gainedHistoryCount = playerHistory?.CardsGained.Count ?? 0;

        try
        {
            // 合练不受同名牌数量等唯一性规则限制。
            // 所有联机端通过相同选择结果确定性执行相同牌组命令。
            using (HeLianScope.Enter())
            {
                await CardPileCmd.RemoveFromDeck(selectedCards, false);

                CardPileAddResult addResult = await CardPileCmd.Add(
                    result,
                    PileType.Deck
                );

                if (!addResult.success)
                {
                    throw new InvalidOperationException(
                        $"合练结果牌 {result.Id} 加入牌组失败。"
                    );
                }
            }

            string materialNames = string.Join(
                " + ",
                selectedCards.Select(card => card.Title)
            );

            _lastLocalVfxCard = result;

            Entry.Logger.Info(
                "合练完成：" +
                $"{string.Join(" + ", selectedCards.Select(card => card.Id))} " +
                $"-> {result.Id}，结果为 {result.GuRank} 转。"
            );

            ShowLocalFeedback(
                success: true,
                "feedback.success",
                ("Materials", materialNames),
                ("Result", result.Title),
                (
                    "Rank",
                    result.GuRank.ToString(CultureInfo.InvariantCulture)
                )
            );

            return true;
        }
        catch (Exception operationException)
        {
            try
            {
                RollBackFailedHeLian(
                    result,
                    materialSnapshots,
                    playerHistory,
                    removedHistoryCount,
                    gainedHistoryCount
                );
            }
            catch (Exception rollbackException)
            {
                Entry.Logger.Info(
                    "合练失败后的牌组回滚也发生异常：" + rollbackException
                );
            }

            Entry.Logger.Info(
                "合练执行失败，已尝试恢复材料与历史记录：" + operationException
            );

            ShowLocalFeedback(
                success: false,
                "feedback.exception",
                ("Message", operationException.Message)
            );

            throw;
        }
    }

    /// <summary>
    /// 合练异常时恢复已从运行状态移除的原卡实例，并撤销本次操作
    /// 追加的牌组历史。回滚使用内部牌堆操作，避免再次触发获得/移除钩子。
    /// </summary>
    private static void RollBackFailedHeLian(
        AbstractGuCard result,
        IReadOnlyList<MaterialSnapshot> materialSnapshots,
        MegaCrit.Sts2.Core.Runs.PlayerMapPointHistoryEntry? playerHistory,
        int removedHistoryCount,
        int gainedHistoryCount
    )
    {
        using (HeLianScope.Enter())
        {
            if (!result.HasBeenRemovedFromState)
            {
                result.RemoveFromState();
            }

            foreach (MaterialSnapshot snapshot in materialSnapshots)
            {
                CardModel card = snapshot.Card;

                if (!card.HasBeenRemovedFromState)
                {
                    continue;
                }

                card.Owner.RunState.AddCard(card, card.Owner);

                int restoreIndex = Math.Clamp(
                    snapshot.DeckIndex,
                    0,
                    card.Owner.Deck.Cards.Count
                );

                card.Owner.Deck.AddInternal(card, restoreIndex);
            }
        }

        if (playerHistory is null)
        {
            return;
        }

        TrimHistory(playerHistory.CardsRemoved, removedHistoryCount);
        TrimHistory(playerHistory.CardsGained, gainedHistoryCount);
    }

    private static void TrimHistory<T>(IList<T> history, int originalCount)
    {
        while (history.Count > originalCount)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private readonly record struct MaterialSnapshot(CardModel Card, int DeckIndex);

    private static bool HasCraftableRecipe(Player player)
    {
        return HeLianRecipeRegistry.TryGetCraftableMaterialCountRange(
            GetAvailableMaterials(player),
            out _,
            out _
        );
    }

    private static CardModel[] GetAvailableMaterials(Player player)
    {
        return player.Deck.Cards.Where(IsEligibleMaterial).ToArray();
    }

    private static bool IsEligibleMaterial(CardModel card)
    {
        // 只有明确出现在某条配方里的蛊牌才能当材料；
        // 合练结果牌若不作为材料出现，会被这一步自然排除。
        return card is IGuCard &&
            card.Pile?.Type == PileType.Deck &&
            HeLianRecipeRegistry.IsEligibleMaterialCard(card);
    }

    private static int GetMaterialSortOrder(CardModel card)
    {
        return card is IGuCard gu ? gu.GuRank : int.MaxValue;
    }
}
