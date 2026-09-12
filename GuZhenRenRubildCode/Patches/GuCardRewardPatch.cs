using System.Reflection;
using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using GuZhenRenRubild.Common.Scoping;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 只在 CardFactory.CreateForReward 调用链中过滤奖励候选，避免污染 Transform、药水或战斗生成。
/// </summary>
internal static class GuCardRewardPatch
{
    private const string HarmonyId = Entry.ModId + ".GuCardReward";

    // 标记"当前调用链正处于奖励候选查询中"，使深层调用也能识别这一次查询的来源。
    private static readonly AmbientFlag RewardCandidateQuery = new();

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo getPossibleCards = RequiredMember.Method(
                typeof(CardCreationOptions),
                nameof(CardCreationOptions.GetPossibleCards),
                [typeof(Player)]
            );
            MethodInfo createForReward = RequiredMember.Method(
                typeof(CardFactory),
                nameof(CardFactory.CreateForReward),
                [typeof(Player), typeof(int), typeof(CardCreationOptions)]
            );
            MethodInfo modifyRewardOptions = RequiredMember.Method(
                typeof(Hook),
                nameof(Hook.TryModifyCardRewardOptions),
                [
                    typeof(IRunState),
                    typeof(Player),
                    typeof(List<CardCreationResult>),
                    typeof(CardCreationOptions),
                    typeof(List<AbstractModel>).MakeByRefType(),
                ]
            );

            harmony.Patch(
                getPossibleCards,
                postfix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(GetPossibleCardsPostfix))
            );
            harmony.Patch(
                createForReward,
                prefix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(CreateForRewardPrefix)),
                postfix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(CreateForRewardPostfix)),
                finalizer: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(CreateForRewardFinalizer))
            );
            harmony.Patch(
                modifyRewardOptions,
                postfix: new HarmonyMethod(typeof(GuCardRewardPatch), nameof(ModifyRewardOptionsPostfix))
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch(RewardCandidateQuery.Reset);
    }

    private static void GetPossibleCardsPostfix(
        Player player,
        ref IEnumerable<CardModel> __result
    )
    {
        if (!RewardCandidateQuery.IsActive)
        {
            return;
        }

        __result = __result
            .GroupBy(static card => card.Id)
            .Select(static group => group.First())
            .Where(card => GuCardRewardRules.CanAppear(player, card))
            .ToArray();
    }

    private static bool CreateForRewardPrefix(
        Player player,
        ref int cardCount,
        CardCreationOptions options,
        ref IEnumerable<CardCreationResult> __result,
        out IDisposable __state
    )
    {
        __state = RewardCandidateQuery.Enter();

        CardModel[] possibleCards = options
            .GetPossibleCards(player)
            .Where(card => GuCardRewardRules.CanAppear(player, card))
            .Where(card =>
                options.RarityOdds != CardRarityOddsType.Uniform ||
                (card.Rarity != CardRarity.Basic && card.Rarity != CardRarity.Ancient)
            )
            .GroupBy(static card => card.Id)
            .Select(static group => group.First())
            .ToArray();

        if (possibleCards.Length == 0)
        {
            Entry.Logger.Info(
                $"Player {player.NetId} has no eligible Gu card reward candidates."
            );
            __result = Array.Empty<CardCreationResult>();
            return false;
        }

        cardCount = Math.Min(cardCount, possibleCards.Length);
        return true;
    }

    private static void CreateForRewardPostfix(IDisposable __state)
    {
        __state.Dispose();
    }

    private static Exception? CreateForRewardFinalizer(
        Exception? __exception,
        IDisposable __state
    )
    {
        __state.Dispose();
        return __exception;
    }

    [HarmonyPriority(Priority.Last)]
    private static void ModifyRewardOptionsPostfix(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        ref bool __result
    )
    {
        int removed = cardRewardOptions.RemoveAll(result =>
            !GuCardRewardRules.CanAppear(player, result.Card)
        );
        if (removed > 0)
        {
            __result = true;
        }
    }
}
