using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Companions;

/// <summary>
/// 永久伴生关系的唯一业务入口。Harmony 只负责在生命周期边界调用这里，
/// 不自行分配 PairId、创建/删除伴生牌或决定 Transform 迁移规则。
/// </summary>
public static class CompanionRelationshipService
{
    internal readonly record struct TransformSnapshot(
        Player Owner,
        int PairId,
        CardModel? Companion,
        Type? CompanionType,
        bool OriginalWasSource
    );

    private static readonly HashSet<Player> Reconciling = [];
    private static readonly object ReconcileLock = new();

    public static void ReconcileDeck(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        lock (ReconcileLock)
        {
            if (!Reconciling.Add(player))
            {
                return;
            }
        }

        try
        {
            ReconcileDeckCore(player);
        }
        finally
        {
            lock (ReconcileLock)
            {
                Reconciling.Remove(player);
            }
        }
    }

    public static bool TryGetCompanion(
        CardModel sourceGu,
        out CardModel companion
    )
    {
        ArgumentNullException.ThrowIfNull(sourceGu);

        if (CompanionNetworkMap.TryGetCompanion(sourceGu, out companion))
        {
            return true;
        }

        companion = null!;
        if (sourceGu is not ICompanionSourceGuCard ||
            sourceGu.Owner == null ||
            CompanionPairState.Get(sourceGu) <= 0)
        {
            return false;
        }

        int pairId = CompanionPairState.Get(sourceGu);
        CardModel? found = sourceGu.Owner.Deck.Cards.FirstOrDefault(card =>
            card is ICompanionCard &&
            CompanionPairState.Get(card) == pairId
        );
        if (found == null)
        {
            return false;
        }

        companion = found;
        return true;
    }

    public static bool TryGetSource(
        CardModel companion,
        out CardModel sourceGu
    )
    {
        ArgumentNullException.ThrowIfNull(companion);

        if (CompanionNetworkMap.TryGetSource(companion, out sourceGu))
        {
            return true;
        }

        sourceGu = null!;
        if (companion is not ICompanionCard ||
            companion.Owner == null ||
            CompanionPairState.Get(companion) <= 0)
        {
            return false;
        }

        int pairId = CompanionPairState.Get(companion);
        CardModel? found = companion.Owner.Deck.Cards.FirstOrDefault(card =>
            card is ICompanionSourceGuCard &&
            CompanionPairState.Get(card) == pairId
        );
        if (found == null)
        {
            return false;
        }

        sourceGu = found;
        return true;
    }

    public static bool IsManagedCompanion(CardModel card)
    {
        return card is ICompanionCard && CompanionPairState.Get(card) > 0;
    }

    public static void RemovePairForSource(CardModel sourceGu)
    {
        if (sourceGu.Owner == null ||
            !TryFindPermanentCompanion(sourceGu, out CardModel companion))
        {
            return;
        }

        RemoveManagedCompanion(companion);
        CompanionPairState.Set(sourceGu, 0);
        CompanionNetworkMap.RebuildDeckMap(sourceGu.Owner);
    }

    internal static TransformSnapshot CaptureBeforeTransformation(
        CardModel original
    )
    {
        Player owner = original.Owner;
        if (original is not ICompanionSourceGuCard)
        {
            return new TransformSnapshot(owner, 0, null, null, false);
        }

        int pairId = CompanionPairState.Get(original);
        TryFindPermanentCompanion(original, out CardModel companion);
        return new TransformSnapshot(
            owner,
            pairId,
            companion,
            companion?.GetType(),
            true
        );
    }

    internal static void ReconcileAfterTransformation(
        TransformSnapshot snapshot,
        CardModel replacement
    )
    {
        if (!snapshot.OriginalWasSource || snapshot.PairId <= 0)
        {
            // 普通牌/无伴生蛊 -> 带伴生蛊，由普通 Reconcile 创建关系。
            return;
        }

        if (replacement is ICompanionSourceGuCard newSource &&
            snapshot.Companion != null &&
            snapshot.CompanionType == newSource.Companion.CardType)
        {
            // 伴生类型不变：把 PairId 转移给新来源，保留原伴生实例、升级和附魔。
            CompanionPairState.Set(replacement, snapshot.PairId);
            CompanionPairState.Set(snapshot.Companion, snapshot.PairId);
            return;
        }

        // 新来源不再需要伴生，或需要不同类型：旧伴生生命周期结束。
        if (snapshot.Companion != null)
        {
            RemoveManagedCompanion(snapshot.Companion);
        }

        CompanionPairState.Set(replacement, 0);
    }

    private static void ReconcileDeckCore(Player player)
    {
        CardPile deck = player.Deck;
        CardModel[] snapshot = deck.Cards.ToArray();
        CardModel[] sources = snapshot
            .Where(static card => card is ICompanionSourceGuCard)
            .ToArray();
        CardModel[] companions = snapshot
            .Where(static card => card is ICompanionCard)
            .ToArray();

        int nextPairId = snapshot
            .Select(CompanionPairState.Get)
            .DefaultIfEmpty(0)
            .Max() + 1;
        HashSet<int> claimedSourcePairs = [];
        bool deckChanged = false;

        // 先修复来源 PairId。永久牌组顺序就是确定性顺序，避免使用随机值或 Guid。
        foreach (CardModel source in sources)
        {
            int pairId = CompanionPairState.Get(source);
            if (pairId <= 0 || !claimedSourcePairs.Add(pairId))
            {
                pairId = nextPairId++;
                CompanionPairState.Set(source, pairId);
                claimedSourcePairs.Add(pairId);
                Entry.Logger.Info(
                    $"[Companion/Reconcile] source={source.Id} pair={pairId} action=AssignPair"
                );
            }
        }

        HashSet<CardModel> usedCompanions = [];
        foreach (CardModel source in sources)
        {
            ICompanionSourceGuCard definition = (ICompanionSourceGuCard)source;
            int pairId = CompanionPairState.Get(source);
            Type expectedType = definition.Companion.CardType;

            CardModel? companion = companions.FirstOrDefault(card =>
                !usedCompanions.Contains(card) &&
                card.GetType() == expectedType &&
                CompanionPairState.Get(card) == pairId
            );

            // 迁移旧存档时优先复用未绑定/孤儿的正确类型伴生，保留升级与附魔。
            companion ??= companions.FirstOrDefault(card =>
                !usedCompanions.Contains(card) &&
                card.GetType() == expectedType &&
                (CompanionPairState.Get(card) <= 0 ||
                 !claimedSourcePairs.Contains(CompanionPairState.Get(card)))
            );

            if (companion == null)
            {
                companion = CreateCompanion(player, source, expectedType, pairId);
                deckChanged = true;
                companions = companions.Append(companion).ToArray();
                Entry.Logger.Info(
                    $"[Companion/Reconcile] source={source.Id} pair={pairId} " +
                    $"action=CreateCompanion companion={companion.Id}"
                );
            }
            else
            {
                CompanionPairState.Set(companion, pairId);
            }

            usedCompanions.Add(companion);
        }

        // 所有没有来源的系统管理伴生都属于孤儿；迁移复用已经在上一步优先完成。
        foreach (CardModel orphan in companions)
        {
            if (usedCompanions.Contains(orphan) ||
                CompanionPairState.Get(orphan) <= 0)
            {
                continue;
            }

            Entry.Logger.Info(
                $"[Companion/Reconcile] pair={CompanionPairState.Get(orphan)} " +
                $"action=RemoveOrphan companion={orphan.Id}"
            );
            RemoveManagedCompanion(orphan, notifyPile: false);
            deckChanged = true;
        }

        if (deckChanged)
        {
            deck.InvokeContentsChanged();
        }

        CompanionNetworkMap.RebuildDeckMap(player);
    }

    private static CardModel CreateCompanion(
        Player player,
        CardModel source,
        Type expectedType,
        int pairId
    )
    {
        if (!typeof(ICompanionCard).IsAssignableFrom(expectedType))
        {
            throw new InvalidOperationException(
                $"Companion type {expectedType.FullName} does not implement {nameof(ICompanionCard)}."
            );
        }

        CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(expectedType));
        using IDisposable scope = CompanionMutationScope.AllowInternalMutation();
        CardModel companion = player.RunState.CreateCard(canonical, player);
        CompanionPairState.Set(companion, pairId);
        companion.FloorAddedToDeck = source.FloorAddedToDeck;

        int sourceIndex = Array.IndexOf(player.Deck.Cards.ToArray(), source);
        int insertIndex = sourceIndex < 0
            ? player.Deck.Cards.Count
            : Math.Min(sourceIndex + 1, player.Deck.Cards.Count);
        player.Deck.AddInternal(companion, insertIndex, silent: true);
        return companion;
    }

    private static bool TryFindPermanentCompanion(
        CardModel source,
        out CardModel companion
    )
    {
        companion = null!;
        if (source.Owner == null)
        {
            return false;
        }

        int pairId = CompanionPairState.Get(source);
        if (pairId <= 0)
        {
            return false;
        }

        CardModel? found = source.Owner.Deck.Cards.FirstOrDefault(card =>
            card is ICompanionCard && CompanionPairState.Get(card) == pairId
        );
        if (found == null)
        {
            return false;
        }

        companion = found;
        return true;
    }

    private static void RemoveManagedCompanion(
        CardModel companion,
        bool notifyPile = true
    )
    {
        using IDisposable scope = CompanionMutationScope.AllowInternalMutation();
        CardPile? pile = companion.Pile;
        companion.RemoveFromCurrentPile(silent: true);
        companion.RemoveFromState();
        CompanionPairState.Set(companion, 0);
        if (notifyPile)
        {
            pile?.InvokeContentsChanged();
        }
    }
}
