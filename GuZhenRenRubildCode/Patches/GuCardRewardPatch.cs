using System.Reflection;
using System.Threading;
using GuZhenRenRubild.Cards.Rules;
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
    private static readonly AsyncLocal<int> RewardCandidateQueryDepth = new();
    private static bool _initialized;

    private sealed class RewardQueryScope(int previousDepth)
    {
        private bool _restored;

        internal void Restore()
        {
            if (_restored)
            {
                return;
            }

            RewardCandidateQueryDepth.Value = previousDepth;
            _restored = true;
        }
    }

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo getPossibleCards = AccessTools.Method(
            typeof(CardCreationOptions),
            nameof(CardCreationOptions.GetPossibleCards),
            [typeof(Player)]
        ) ?? throw new MissingMethodException(typeof(CardCreationOptions).FullName, nameof(CardCreationOptions.GetPossibleCards));
        MethodInfo createForReward = AccessTools.Method(
            typeof(CardFactory),
            nameof(CardFactory.CreateForReward),
            [typeof(Player), typeof(int), typeof(CardCreationOptions)]
        ) ?? throw new MissingMethodException(typeof(CardFactory).FullName, nameof(CardFactory.CreateForReward));
        MethodInfo modifyRewardOptions = AccessTools.Method(
            typeof(Hook),
            nameof(Hook.TryModifyCardRewardOptions),
            [
                typeof(IRunState),
                typeof(Player),
                typeof(List<CardCreationResult>),
                typeof(CardCreationOptions),
                typeof(List<AbstractModel>).MakeByRefType(),
            ]
        ) ?? throw new MissingMethodException(typeof(Hook).FullName, nameof(Hook.TryModifyCardRewardOptions));

        Harmony harmony = new(HarmonyId);
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
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        RewardCandidateQueryDepth.Value = 0;
        _initialized = false;
    }

    private static void GetPossibleCardsPostfix(
        Player player,
        ref IEnumerable<CardModel> __result
    )
    {
        if (RewardCandidateQueryDepth.Value <= 0)
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
        out RewardQueryScope __state
    )
    {
        __state = new RewardQueryScope(RewardCandidateQueryDepth.Value);
        RewardCandidateQueryDepth.Value++;

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

    private static void CreateForRewardPostfix(RewardQueryScope __state)
    {
        __state.Restore();
    }

    private static Exception? CreateForRewardFinalizer(
        Exception? __exception,
        RewardQueryScope __state
    )
    {
        __state.Restore();
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
