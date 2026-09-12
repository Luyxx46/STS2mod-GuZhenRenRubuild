using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Companions;

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

    internal static bool TryGetCompanion(CardModel source, out CardModel companion) =>
        TryResolveMappedCard(
            source,
            static map => map.DeckForward,
            static map => map.CombatForward,
            out companion
        );

    internal static bool TryGetSource(CardModel companion, out CardModel source) =>
        TryResolveMappedCard(
            companion,
            static map => map.DeckReverse,
            static map => map.CombatReverse,
            out source
        );

    /// <summary>
    /// 按当前牌所在区域选择映射：永久牌组用 DeckIndex，战斗牌用战斗网络编号。
    /// 正向（来源→伴生）与反向（伴生→来源）只差传入的字典，因此共用一套查找流程。
    /// </summary>
    private static bool TryResolveMappedCard(
        CardModel anchor,
        Func<PlayerMap, Dictionary<uint, uint>> deckMapSelector,
        Func<PlayerMap, Dictionary<uint, uint>> combatMapSelector,
        out CardModel mapped
    )
    {
        mapped = null!;

        Player? owner = anchor.Owner;
        if (owner == null || !Maps.TryGetValue(owner, out PlayerMap? map))
        {
            return false;
        }

        if (anchor.Pile?.Type == PileType.Deck)
        {
            uint anchorDeckIndex = NetDeckCard.FromModel(anchor).DeckIndex;
            if (!deckMapSelector(map).TryGetValue(
                    anchorDeckIndex,
                    out uint mappedDeckIndex
                ))
            {
                return false;
            }

            mapped = new NetDeckCardAccessor(mappedDeckIndex).ToCardModel(owner);
            return true;
        }

        if (anchor.Pile?.IsCombatPile == true &&
            NetCombatCardDb.Instance.TryGetCardId(
                anchor,
                out uint anchorCombatId
            ) &&
            combatMapSelector(map).TryGetValue(
                anchorCombatId,
                out uint mappedCombatId
            ) &&
            NetCombatCardDb.Instance.TryGetCard(
                mappedCombatId,
                out CardModel? combatCard
            ) &&
            combatCard != null)
        {
            mapped = combatCard;
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
