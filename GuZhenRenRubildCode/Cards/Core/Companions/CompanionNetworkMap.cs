using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Companions;

/// <summary>
/// PairId 是持久身份；DeckIndex / CombatCardId 只缓存在当前运行时作用域。
/// </summary>
public static class CompanionNetworkMap
{
    private sealed class PlayerMap
    {
        internal readonly Dictionary<uint, uint> DeckForward = [];
        internal readonly Dictionary<uint, uint> DeckReverse = [];
        internal readonly Dictionary<uint, uint> CombatForward = [];
        internal readonly Dictionary<uint, uint> CombatReverse = [];
    }

    private static readonly ConditionalWeakTable<Player, PlayerMap> Maps = new();

    internal static void RebuildDeckMap(Player player)
    {
        PlayerMap map = Maps.GetOrCreateValue(player);
        map.DeckForward.Clear();
        map.DeckReverse.Clear();

        foreach (CardModel source in player.Deck.Cards.Where(
                     static card => card is ICompanionSourceGuCard))
        {
            int pairId = CompanionPairState.Get(source);
            if (pairId <= 0)
            {
                continue;
            }

            CardModel? companion = player.Deck.Cards.FirstOrDefault(card =>
                card is ICompanionCard && CompanionPairState.Get(card) == pairId
            );
            if (companion == null)
            {
                continue;
            }

            uint sourceId = NetDeckCard.FromModel(source).DeckIndex;
            uint companionId = NetDeckCard.FromModel(companion).DeckIndex;
            map.DeckForward[sourceId] = companionId;
            map.DeckReverse[companionId] = sourceId;
        }
    }

    internal static void RebuildCombatMap(Player player)
    {
        PlayerMap map = Maps.GetOrCreateValue(player);
        map.CombatForward.Clear();
        map.CombatReverse.Clear();

        CardModel[] combatCards = player.PlayerCombatState?.AllCards
            .Distinct()
            .ToArray() ?? [];

        foreach (CardModel combatSource in combatCards.Where(
                     static card => card is ICompanionSourceGuCard && card.DeckVersion != null))
        {
            CardModel permanentSource = combatSource.DeckVersion!;
            int pairId = CompanionPairState.Get(permanentSource);
            if (pairId <= 0)
            {
                continue;
            }

            CardModel? permanentCompanion = player.Deck.Cards.FirstOrDefault(card =>
                card is ICompanionCard && CompanionPairState.Get(card) == pairId
            );
            if (permanentCompanion == null)
            {
                continue;
            }

            CardModel? combatCompanion = combatCards.FirstOrDefault(card =>
                ReferenceEquals(card.DeckVersion, permanentCompanion)
            );
            if (combatCompanion == null ||
                !NetCombatCardDb.Instance.TryGetCardId(combatSource, out uint sourceId) ||
                !NetCombatCardDb.Instance.TryGetCardId(combatCompanion, out uint companionId))
            {
                continue;
            }

            map.CombatForward[sourceId] = companionId;
            map.CombatReverse[companionId] = sourceId;
            Entry.Logger.Info(
                $"[Companion/CombatMap] sourceCombatId={sourceId} companionCombatId={companionId}"
            );
        }
    }

    internal static bool TryGetCompanion(CardModel source, out CardModel companion)
    {
        companion = null!;
        Player? owner = source.Owner;
        if (owner == null || !Maps.TryGetValue(owner, out PlayerMap? map))
        {
            return false;
        }

        if (source.Pile?.Type == PileType.Deck)
        {
            uint sourceId = NetDeckCard.FromModel(source).DeckIndex;
            if (!map.DeckForward.TryGetValue(sourceId, out uint companionId))
            {
                return false;
            }

            companion = new NetDeckCardAccessor(companionId).ToCardModel(owner);
            return true;
        }

        if (source.Pile?.IsCombatPile == true &&
            NetCombatCardDb.Instance.TryGetCardId(source, out uint combatSourceId) &&
            map.CombatForward.TryGetValue(combatSourceId, out uint combatCompanionId) &&
            NetCombatCardDb.Instance.TryGetCard(combatCompanionId, out CardModel? combatCompanion) &&
            combatCompanion != null)
        {
            companion = combatCompanion;
            return true;
        }

        return false;
    }

    internal static bool TryGetSource(CardModel companion, out CardModel source)
    {
        source = null!;
        Player? owner = companion.Owner;
        if (owner == null || !Maps.TryGetValue(owner, out PlayerMap? map))
        {
            return false;
        }

        if (companion.Pile?.Type == PileType.Deck)
        {
            uint companionId = NetDeckCard.FromModel(companion).DeckIndex;
            if (!map.DeckReverse.TryGetValue(companionId, out uint sourceId))
            {
                return false;
            }

            source = new NetDeckCardAccessor(sourceId).ToCardModel(owner);
            return true;
        }

        if (companion.Pile?.IsCombatPile == true &&
            NetCombatCardDb.Instance.TryGetCardId(companion, out uint combatCompanionId) &&
            map.CombatReverse.TryGetValue(combatCompanionId, out uint combatSourceId) &&
            NetCombatCardDb.Instance.TryGetCard(combatSourceId, out CardModel? combatSource) &&
            combatSource != null)
        {
            source = combatSource;
            return true;
        }

        return false;
    }

    // NetDeckCard 的 DeckIndex setter 为私有；读取映射时直接按其语义索引永久牌组即可。
    private readonly record struct NetDeckCardAccessor(uint DeckIndex)
    {
        internal CardModel ToCardModel(Player player) =>
            player.Deck.Cards[checked((int)DeckIndex)];
    }
}
