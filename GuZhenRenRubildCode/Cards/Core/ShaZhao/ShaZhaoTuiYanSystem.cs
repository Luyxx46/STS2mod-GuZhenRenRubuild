using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Combat;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 杀招推演。
///
/// 推演入口是"杀招推演"系统牌：先选目标杀招，再选与该目标匹配的材料，
/// 材料与元气都满足时才封装材料并生成杀招。取消或失败不消耗任何资源，
/// 材料留在蛊牌堆。
///
/// 材料只在"正常用完"或"杀招被异常移出战斗"时返还（见
/// <see cref="ShaZhaoBindingService.FinalizeAsync"/>）：**玩家侧没有主动解体入口**。
/// </summary>
internal static class ShaZhaoTuiYanSystem
{
    // 推演界面的提示与失败原因都放在原版提示表中，键名统一带本模组前缀。
    private const string LocTable = "static_hover_tips";
    private const string LocKeyPrefix = "GU_ZHEN_REN_RUBILD_SHA_ZHAO.";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        // 本系统目前没有任何需要注册的入口（右键解体已移除），
        // 保留这一对方法只是维持 Entry.RuntimeComponents 的组件契约，
        // 后续若再挂新入口（例如新的选牌流程）在这里登记即可。
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        _initialized = false;
    }

    /// <summary>
    /// 打出"杀招推演"系统牌的推演入口。
    ///
    /// 成功返回 true（推演牌应当消耗），且只有成功收口时才实际消耗元气；
    /// 取消、配方无效、资源不足或其他正常失败返回 false，
    /// 由推演牌负责回到手牌并回滚原生能量/星星支付。
    /// </summary>
    internal static async Task<bool> DeriveAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardPlay derivationPlay
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(derivationPlay);

        if (player.PlayerCombatState is null)
        {
            return false;
        }

        (Type? targetCardType, List<CardModel> materials) =
            await SelectTargetAndMaterialsAsync(choiceContext, player);

        if (targetCardType is null || materials.Count == 0)
        {
            return false;
        }

        if (!ShaZhaoRecipeRegistry.HasMatchingRecipe(
                materials,
                targetCardType
            ))
        {
            ShowFailure(player, "invalidRecipe");
            return false;
        }

        int yuanQiCost = CalculateYuanQiCost(materials);

        if (SecondaryResourceCmd.Get(player, YuanQiSystem.ResourceId) <
            yuanQiCost)
        {
            ShowFailure(player, "insufficientYuanQi");
            return false;
        }

        if (!ShaZhaoRecipeRegistry.TryCreateResultForTarget(
                materials,
                player,
                targetCardType,
                out AbstractShaZhaoCard? shaZhao
            ))
        {
            ShowFailure(player, "creationFailed");
            return false;
        }

        // 推演出的杀招首次打出不消耗能量。
        shaZhao.EnergyCost.SetUntilPlayed(0);

        if (!await AddToHandAsync(shaZhao, player))
        {
            await RemoveUncommittedResultAsync(shaZhao);
            ShowFailure(player, "handFull");
            return false;
        }

        // 记录支付前的元气，供封装阶段出现异常时原额退回。
        int yuanQiBeforeSpend = SecondaryResourceCmd.Get(
            player,
            YuanQiSystem.ResourceId
        );

        bool paid = yuanQiCost == 0 || await SecondaryResourceCmd.Spend(
            player,
            YuanQiSystem.ResourceId,
            yuanQiCost,
            card: shaZhao,
            source: shaZhao
        );

        if (!paid)
        {
            await RemoveUncommittedResultAsync(shaZhao);
            ShowFailure(player, "insufficientYuanQi");
            return false;
        }

        // 封装是"付费之后"的多步操作。任一步抛出异常都必须收口，
        // 否则会同时留下已经扣掉的元气、已经登记的结果牌，
        // 以及可能只封装了一半的材料。
        try
        {
            await shaZhao.BindMaterialsAsync(materials);
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"[杀招推演] 封装材料时发生异常，已回滚本次推演：{exception}"
            );

            await ShaZhaoBindingService.FinalizeAsync(
                shaZhao,
                player,
                ShaZhaoBindingService.FinalizeReason.AbnormalRemoval
            );
            await RemoveUncommittedResultAsync(shaZhao);

            if (yuanQiCost > 0)
            {
                await SecondaryResourceCmd.Set(
                    player,
                    YuanQiSystem.ResourceId,
                    yuanQiBeforeSpend,
                    source: shaZhao
                );
            }

            ShowFailure(player, "creationFailed");
            return false;
        }

        await GuCardPileSystem.RefillActiveAsync(player, skipVisuals: false);

        // 登记本次成功推演：八转起会在这里补发第二张推演牌。
        await ApertureSystem.RegisterShaZhaoDerivationAsync(player);

        Entry.Logger.Info(
            $"[杀招推演] 成功推演 {shaZhao.GetType().Name}：" +
            $"原生能量 {derivationPlay.Resources.EnergySpent}、" +
            $"元气 {yuanQiCost}、材料 {materials.Count} 张、" +
            $"转数 {shaZhao.GuRank}。"
        );

        return true;
    }

    // =================================================================
    //  费用
    // =================================================================

    /// <summary>
    /// 推演费用 = 所有材料元气消耗的算术平均值，向上取整。
    /// 元气是整数资源，因此非整数结果统一进位；
    /// 材料越多、单张越便宜，推演越划算。
    /// </summary>
    private static int CalculateYuanQiCost(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count == 0)
        {
            return 0;
        }

        long totalCost = materials.Sum(static card =>
            (long)Math.Max(0, card is IGuCard gu ? gu.YuanQiCost : 0)
        );

        return (int)Math.Min(
            int.MaxValue,
            (totalCost + materials.Count - 1) / materials.Count
        );
    }

    // =================================================================
    //  选择流程
    // =================================================================

    /// <summary>
    /// 先选目标杀招，再选与该目标匹配的材料。
    /// 材料只在蛊手牌与蛊待命堆中挑选；蛊冷却堆与蛊封存区不参与推演。
    /// </summary>
    private static async Task<(
        Type? TargetCardType,
        List<CardModel> Materials
    )> SelectTargetAndMaterialsAsync(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        CardModel[] availableMaterials = GetAvailableMaterials(player);

        Type[] craftableResultTypes = ShaZhaoRecipeRegistry
            .GetCraftableResultTypes(availableMaterials)
            .ToArray();

        if (craftableResultTypes.Length == 0)
        {
            ShowFailure(player, "noRecipe");
            return (null, []);
        }

        // 预览牌必须显式绑定 Owner：战斗选卡界面会从第一张候选牌初始化牌堆。
        CardModel[] targetPreviews = craftableResultTypes
            .Select(resultType =>
                ShaZhaoCardCatalog.CreateOwnedPreview(resultType, player)
            )
            .ToArray();

        CardModel? target = (
            await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                targetPreviews,
                player,
                new CardSelectorPrefs(GetLoc("targetSelectionPrompt"), 1)
                {
                    Cancelable = true,
                    // 即使当前只能推演出一张杀招，也进入选择界面由玩家明确点击。
                    RequireManualConfirmation = true,
                    PretendCardsCanBePlayed = true,
                }
            )
        ).FirstOrDefault();

        if (target == null)
        {
            return (null, []);
        }

        Type targetCardType = target.GetType();
        IReadOnlyList<Type> materialTypes =
            ShaZhaoRecipeRegistry.GetMaterialTypesForResult(
                targetCardType,
                out _
            );

        if (!ShaZhaoRecipeRegistry.GetMaterialCountRangeForResult(
                targetCardType,
                out int minimumMaterialCount,
                out int maximumMaterialCount
            ))
        {
            return (null, []);
        }

        CardModel[] choices = availableMaterials
            .Where(card =>
                materialTypes.Contains(card.GetType()) &&
                ShaZhaoRecipeRegistry.IsEligibleMaterialCardForResult(
                    card,
                    targetCardType
                )
            )
            .OrderBy(
                static card => card.Id.ToString(),
                StringComparer.Ordinal
            )
            .ThenByDescending(AbstractShaZhaoCard.GetMaterialRank)
            .ToArray();

        if (choices.Length < minimumMaterialCount)
        {
            ShowFailure(player, "notEnoughMaterials");
            return (targetCardType, []);
        }

        LocString materialPrompt = GetLoc("selectionPrompt");
        materialPrompt.Add("TargetName", target.Title);

        List<CardModel> selected = (
            await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                choices,
                player,
                new CardSelectorPrefs(
                    materialPrompt,
                    minimumMaterialCount,
                    maximumMaterialCount
                )
                {
                    Cancelable = true,
                    RequireManualConfirmation =
                        minimumMaterialCount != maximumMaterialCount,
                    PretendCardsCanBePlayed = true,
                }
            )
        ).ToList();

        if (selected.Count < minimumMaterialCount ||
            selected.Count > maximumMaterialCount)
        {
            selected.Clear();
        }

        return (targetCardType, selected);
    }

    /// <summary>
    /// 推演候选材料：蛊手牌与蛊待命堆中仍可催动、且尚未被封装的蛊牌。
    /// </summary>
    private static CardModel[] GetAvailableMaterials(Player player)
    {
        return GuCardPileSystem.ActivePileType
            .GetPile(player)
            .Cards
            .Concat(GuCardPileSystem.StoragePileType.GetPile(player).Cards)
            .Distinct()
            .Where(static card =>
                card is IGuCard &&
                GuCardRuntime.CanUse(card) &&
                !ShaZhaoBindingService.HasMaterialBinding(card))
            .ToArray();
    }

    // =================================================================
    //  结果牌入牌堆
    // =================================================================

    private static async Task<bool> AddToHandAsync(
        AbstractShaZhaoCard shaZhao,
        Player player
    )
    {
        await CardPileCmd.AddGeneratedCardToCombat(
            shaZhao,
            PileType.Hand,
            player
        );

        return shaZhao.Pile?.Type == PileType.Hand;
    }

    /// <summary>
    /// 移除一张已经登记进战斗状态、但最终没能交付给玩家的结果牌，
    /// 避免留下不可见的悬空卡牌。
    /// </summary>
    private static async Task RemoveUncommittedResultAsync(
        AbstractShaZhaoCard shaZhao
    )
    {
        if (shaZhao.Pile != null)
        {
            await CardPileCmd.RemoveFromCombat(shaZhao, skipVisuals: true);
            return;
        }

        ICombatState? combatState = shaZhao.CombatState;
        shaZhao.RemoveFromState();
        combatState?.RemoveCard(shaZhao);
    }

    // =================================================================
    //  本地反馈
    // =================================================================

    /// <summary>
    /// 失败只影响发起玩家的界面；材料尚未移动，因此无需补偿命令。
    /// </summary>
    private static void ShowFailure(Player player, string reason)
    {
        Entry.Logger.Info(
            $"[杀招推演] 推演失败（{reason}）：未消耗费用，材料保留在蛊牌堆。"
        );

        if (!LocalContext.IsMe(player))
        {
            return;
        }

        NModalContainer? container = NModalContainer.Instance;

        if (container == null || container.OpenModal != null)
        {
            return;
        }

        NErrorPopup? popup = NErrorPopup.Create(
            GetRawLoc("failureTitle"),
            GetRawLoc(reason),
            showReportBugButton: false
        );

        if (popup != null)
        {
            container.Add(popup);
        }
    }

    private static LocString GetLoc(string keySuffix) =>
        new(LocTable, LocKeyPrefix + keySuffix);

    private static string GetRawLoc(string keySuffix) =>
        GetLoc(keySuffix).GetRawText();
}
