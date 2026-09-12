using System.Reflection;
using GuZhenRenRubild.Cards.Companions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 把永久牌组增删、Transform、新游戏、读档和联机同步接入伴生关系修复。
/// </summary>
internal static class CompanionDeckLifecyclePatch
{
    private const string HarmonyId = Entry.ModId + ".CompanionDeckLifecycle";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    private sealed record TransformEntryState(
        CompanionRelationshipService.TransformSnapshot PairState,
        PileType PileType,
        int PileIndex
    );

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo add = RequiredMember.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Add),
                [
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool),
                    // 该重载还带有 isChangingOwners；反射签名必须与运行时参数列表逐位一致，否则查不到方法。
                    typeof(bool),
                ]
            );
            MethodInfo remove = RequiredMember.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.RemoveFromDeck),
                [typeof(IReadOnlyList<CardModel>), typeof(bool)]
            );
            MethodInfo transform = RequiredMember.Method(
                typeof(CardCmd),
                nameof(CardCmd.Transform),
                [typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle)]
            );
            MethodInfo newRun = RequiredMember.Method(
                typeof(RunState), nameof(RunState.CreateForNewRun)
            );
            MethodInfo loadRun = RequiredMember.Method(
                typeof(RunState), nameof(RunState.FromSerializable), [typeof(SerializableRun)]
            );
            MethodInfo syncPlayer = RequiredMember.Method(
                typeof(Player), nameof(Player.SyncWithSerializedPlayer), [typeof(SerializablePlayer)]
            );

            harmony.Patch(
                add,
                prefix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(AddPrefix)),
                postfix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(AddPostfix))
            );
            harmony.Patch(
                remove,
                prefix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(RemovePrefix)),
                postfix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(RemovePostfix))
            );
            harmony.Patch(
                transform,
                prefix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(TransformPrefix)),
                postfix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(TransformPostfix))
            );
            harmony.Patch(
                newRun,
                postfix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(RunCreatedPostfix))
            );
            harmony.Patch(
                loadRun,
                postfix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(RunCreatedPostfix))
            );
            harmony.Patch(
                syncPlayer,
                postfix: new HarmonyMethod(typeof(CompanionDeckLifecyclePatch), nameof(PlayerSyncedPostfix))
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static void AddPrefix(
        ref IEnumerable<CardModel> cards,
        CardPile newPile,
        out Player? __state
    )
    {
        CardModel[] materialized = cards.ToArray();
        cards = materialized;
        __state = newPile.Type == PileType.Deck
            ? materialized.FirstOrDefault()?.Owner
            : null;
    }

    private static void AddPostfix(
        Player? __state,
        ref Task<IReadOnlyList<CardPileAddResult>> __result
    )
    {
        if (__state != null)
        {
            __result = ReconcileAfter(__result, __state);
        }
    }

    private static async Task<IReadOnlyList<CardPileAddResult>> ReconcileAfter(
        Task<IReadOnlyList<CardPileAddResult>> task,
        Player player
    )
    {
        IReadOnlyList<CardPileAddResult> result = await task;
        CompanionRelationshipService.ReconcileDeck(player);
        return result;
    }

    private static void RemovePrefix(
        IReadOnlyList<CardModel> cards,
        out Player[] __state
    )
    {
        __state = cards
            .Select(static card => card.Owner)
            .Distinct()
            .ToArray();
    }

    private static void RemovePostfix(Player[] __state, ref Task __result)
    {
        __result = ReconcileAfter(__result, __state);
    }

    private static async Task ReconcileAfter(Task task, IEnumerable<Player> players)
    {
        await task;
        foreach (Player player in players.Distinct())
        {
            CompanionRelationshipService.ReconcileDeck(player);
        }
    }

    private static void TransformPrefix(
        ref IEnumerable<CardTransformation> transformations,
        out TransformEntryState[] __state
    )
    {
        CardTransformation[] materialized = transformations.ToArray();
        transformations = materialized;
        __state = materialized
            .Select(transformation =>
            {
                CardPile? pile = transformation.Original.Pile;
                return new TransformEntryState(
                    CompanionRelationshipService.CaptureBeforeTransformation(
                        transformation.Original
                    ),
                    pile?.Type ?? PileType.None,
                    pile == null
                        ? -1
                        : Array.IndexOf(pile.Cards.ToArray(), transformation.Original)
                );
            })
            .OrderBy(static item => item.PileType)
            .ThenBy(static item => item.PileIndex)
            .ToArray();
    }

    private static void TransformPostfix(
        TransformEntryState[] __state,
        ref Task<IEnumerable<CardPileAddResult>> __result
    )
    {
        __result = ReconcileAfterTransform(__result, __state);
    }

    private static async Task<IEnumerable<CardPileAddResult>> ReconcileAfterTransform(
        Task<IEnumerable<CardPileAddResult>> task,
        TransformEntryState[] states
    )
    {
        CardPileAddResult[] results = (await task).ToArray();
        HashSet<Player> affectedPlayers = [];
        int count = Math.Min(states.Length, results.Length);
        for (int index = 0; index < count; index++)
        {
            TransformEntryState state = states[index];
            if (state.PileType != PileType.Deck || !results[index].success)
            {
                continue;
            }

            CompanionRelationshipService.ReconcileAfterTransformation(
                state.PairState,
                results[index].cardAdded
            );
            affectedPlayers.Add(state.PairState.Owner);
        }

        foreach (Player player in affectedPlayers)
        {
            CompanionRelationshipService.ReconcileDeck(player);
        }

        return results;
    }

    private static void RunCreatedPostfix(RunState __result)
    {
        foreach (Player player in __result.Players)
        {
            CompanionRelationshipService.ReconcileDeck(player);
        }
    }

    private static void PlayerSyncedPostfix(Player __instance)
    {
        CompanionRelationshipService.ReconcileDeck(__instance);
    }
}
