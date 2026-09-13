using System.Reflection;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 战斗边界适配器：战前修复永久配对，战斗网络编号完成后建立映射并收纳蛊牌，
/// 原生首次抽牌完成后再播放开场蛊手牌入场。
/// </summary>
internal static class GuCombatPatch
{
    private const string HarmonyId = Entry.ModId + ".GuCombat";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo populateCombatState = RequiredMember.DeclaredMethod(
                typeof(Player),
                nameof(Player.PopulateCombatState),
                [typeof(Rng), typeof(CombatState)]
            );
            MethodInfo startCombat = RequiredMember.DeclaredMethod(
                typeof(NetCombatCardDb),
                nameof(NetCombatCardDb.StartCombat),
                [typeof(IReadOnlyList<Player>)]
            );
            MethodInfo drawInternal = RequiredMember.DeclaredMethod(
                typeof(CardPileCmd),
                "DrawInternal",
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool),
                ]
            );

            if (drawInternal.ReturnType != typeof(Task<IEnumerable<CardModel>>))
            {
                throw new MissingMethodException("CardPileCmd.DrawInternal has an unexpected return type.");
            }

            harmony.Patch(
                populateCombatState,
                prefix: new HarmonyMethod(typeof(GuCombatPatch), nameof(PopulateCombatStatePrefix)),
                postfix: new HarmonyMethod(typeof(GuCombatPatch), nameof(PopulateCombatStatePostfix))
            );
            harmony.Patch(
                startCombat,
                postfix: new HarmonyMethod(typeof(GuCombatPatch), nameof(StartCombatPostfix))
            );
            harmony.Patch(
                drawInternal,
                postfix: new HarmonyMethod(typeof(GuCombatPatch), nameof(DrawInternalPostfix))
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static void PopulateCombatStatePrefix(Player __instance)
    {
        CompanionRelationshipService.ReconcileDeck(__instance);
    }

    private static void PopulateCombatStatePostfix(Player __instance)
    {
        foreach (AbstractGuCard card in EnumerateNativeCombatPiles(__instance))
        {
            card.RefreshRankDerivedState();
        }
    }

    private static void StartCombatPostfix(IReadOnlyList<Player> players)
    {
        foreach (Player player in players)
        {
            CompanionNetworkMap.RebuildCombatMap(player);
            GuCardPileSystem.InitializeGuCardsForCombat(player);
        }
    }

    private static void DrawInternalPostfix(
        Player player,
        bool fromHandDraw,
        ref Task<IEnumerable<CardModel>> __result
    )
    {
        if (!fromHandDraw || player.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        __result = AwaitDrawThenGuEntryAsync(__result, player, fromHandDraw);
    }

    private static async Task<IEnumerable<CardModel>> AwaitDrawThenGuEntryAsync(
        Task<IEnumerable<CardModel>> drawTask,
        Player player,
        bool fromHandDraw
    )
    {
        IEnumerable<CardModel> drawnCards = await drawTask;
        Task? entryTask = GuCardPileSystem.BeginOpeningGuEntry(player, fromHandDraw);
        if (entryTask != null)
        {
            await entryTask;
        }

        return drawnCards;
    }

    private static IEnumerable<AbstractGuCard> EnumerateNativeCombatPiles(Player player)
    {
        return PileType.Draw.GetPile(player).Cards
            .Concat(PileType.Discard.GetPile(player).Cards)
            .Concat(PileType.Hand.GetPile(player).Cards)
            .OfType<AbstractGuCard>();
    }
}
