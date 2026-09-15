using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Abstractions;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards.Core.Rules;

/// <summary>
/// 仙蛊唯一性的纯规则层。
///
/// 六转及以上（<see cref="ApertureProgression.ImmortalRank"/>）的蛊牌视为仙蛊；
/// **同名**（<see cref="CardModel.Id"/> 相同）仙蛊在整局范围内唯一，即扫描全部玩家的
/// 永久牌组而不只看当前玩家。规则覆盖两条入口：
/// <list type="bullet">
/// <item>升转：把一只蛊牌升到六转及以上时先做唯一性仲裁，牌组中已有同名仙蛊则不升转；</item>
/// <item>入牌：任何卡牌进入永久牌组时（<c>Hook.ShouldAddToDeck</c>）再次仲裁，
/// 冲突时拒绝入牌，或按确定性优先级把落败的既有同名仙蛊降回五转。</item>
/// </list>
///
/// 仲裁优先级按"首次成为仙蛊的楼层 → 玩家槽位 → 牌组位置 → 玩家 NetId"比较，
/// 因此即使各联机端先后处理两个玩家的消息，最终胜者也相同。
///
/// 判定刻意使用具体基类 <see cref="AbstractGuCard"/> 而不是 <c>IGuCard</c>：
/// 只有它同时拥有可写转数与 <see cref="SavedAttachedState{TModel,TValue}"/> 持久化，
/// 降转才有落点，规则的不变式才守得住。
/// </summary>
public static class GuXianGuRules
{
    /// <summary>仙蛊的起始转数，与空窍的仙窍阶段保持一致。</summary>
    public const int XianGuRank = ApertureProgression.ImmortalRank;

    // 唯一性仲裁与"首次成仙楼层"的写入必须在同一临界区内完成，
    // 否则两个玩家同时升转时会各自认为自己获胜。
    private static readonly object MutationSync = new();

    // 0 表示尚未登记；其他值保存"首次成为仙蛊的楼层 + 1"。
    // 未登记的既有仙蛊（旧存档、外部工具产生的牌）优先级取 int.MinValue，
    // 即被视作最早产生，绝不会被新仙蛊顶替。
    private static readonly SavedAttachedState<CardModel, int> ClaimFloorState = new(
        Entry.ModId + ".xian_gu_claim_floor",
        static () => 0
    );

    /// <summary>这张牌当前是否为仙蛊（六转及以上的蛊牌）。</summary>
    public static bool IsXianGu(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is AbstractGuCard gu && gu.GuRank >= XianGuRank;
    }

    /// <summary>
    /// 整局范围内是否已经存在与 <paramref name="candidate"/> 同名的仙蛊。
    /// 用于奖励赋阶封顶等"入牌前"的判定；<paramref name="ignoredCard"/> 用于排除一张已知的例外。
    /// </summary>
    public static bool HasSameXianGu(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredCard = null
    )
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(candidate);

        return FindConflicts(
            runState,
            candidate,
            ignoredCard,
            ignoredCards: null
        ).Length > 0;
    }

    /// <summary>
    /// 整局范围内是否已经存在与 <paramref name="candidate"/> 同名的仙蛊，
    /// 且该仙蛊不属于 <paramref name="ignoredCards"/>（例如本次合练即将消耗掉的材料）。
    /// </summary>
    public static bool HasSameXianGu(
        IRunState runState,
        CardModel candidate,
        IReadOnlySet<CardModel> ignoredCards
    )
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(ignoredCards);

        return FindConflicts(
            runState,
            candidate,
            ignoredCard: null,
            ignoredCards
        ).Length > 0;
    }

    /// <summary>
    /// <paramref name="candidate"/> 能否升到 <paramref name="targetRank"/>。
    ///
    /// 只在"升到六转及以上、且候选牌就在永久牌组里"时参与仲裁：
    /// 战斗内临时牌、预览克隆（不在牌组中）与六转以下的升转都不受影响。
    /// </summary>
    public static bool CanReachGuRank(CardModel candidate, int targetRank)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (targetRank < XianGuRank ||
            candidate.Pile?.Type != PileType.Deck)
        {
            return true;
        }

        if (candidate.Owner?.RunState is not { } runState)
        {
            return true;
        }

        lock (MutationSync)
        {
            return CanCandidateOwnXianGuClaim(
                runState,
                candidate,
                ignoredCard: null,
                ignoredCards: null
            );
        }
    }

    /// <summary>
    /// 把"检查整局唯一性"和"写入新转数"放在同一个临界区：仲裁失败时
    /// <paramref name="commit"/> 不会被调用，调用方可以据此保持原状。
    /// </summary>
    internal static bool TryCommitGuRankIncrease(
        CardModel candidate,
        int targetRank,
        Action commit
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(commit);

        if (targetRank < XianGuRank ||
            candidate.Pile?.Type != PileType.Deck)
        {
            commit();
            return true;
        }

        if (candidate.Owner?.RunState is not { } runState)
        {
            commit();
            return true;
        }

        lock (MutationSync)
        {
            if (!CanCandidateOwnXianGuClaim(
                    runState,
                    candidate,
                    ignoredCard: null,
                    ignoredCards: null
                ))
            {
                Entry.Logger.Info(
                    $"仙蛊唯一性仲裁：阻止 {candidate.Id} 升至 " +
                    $"{targetRank} 转——整局中已有同名仙蛊。"
                );
                return false;
            }

            // 先提交候选牌；若其自身升转回调抛出，不应提前修改
            // 已存在的仙蛊。提交成功后再执行无异步的确定性冲突修复。
            commit();

            ReconcileConflictingXianGu(
                runState,
                candidate,
                ignoredCard: null,
                ignoredCards: null
            );
            RegisterXianGuClaimUnsafe(
                candidate,
                runState.TotalFloor
            );
            return true;
        }
    }

    /// <summary>
    /// 永久牌组入口的仲裁：候选牌本身不是仙蛊时直接放行（本模组没有普通
    /// "唯一"关键词体系）；是仙蛊时按与升转相同的确定性优先级决定去留。
    /// </summary>
    internal static bool TryAuthorizePermanentDeckEntry(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredExistingCard = null
    )
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!IsXianGu(candidate))
        {
            return true;
        }

        lock (MutationSync)
        {
            if (!CanCandidateOwnXianGuClaim(
                    runState,
                    candidate,
                    ignoredExistingCard,
                    ignoredCards: null
                ))
            {
                Entry.Logger.Info(
                    $"仙蛊唯一性仲裁：拒绝 {candidate.Id} 加入永久牌组——" +
                    "整局中已有同名仙蛊。"
                );
                return false;
            }

            ReconcileConflictingXianGu(
                runState,
                candidate,
                ignoredExistingCard,
                ignoredCards: null
            );
            RegisterXianGuClaimUnsafe(
                candidate,
                runState.TotalFloor
            );
            return true;
        }
    }

    /// <summary>
    /// 登记"首次成为仙蛊的楼层"。非仙蛊、已登记过的牌都不会改变既有记录，
    /// 因此重复调用是安全的。
    /// </summary>
    internal static void RegisterXianGuClaim(
        CardModel candidate,
        int floor
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!IsXianGu(candidate))
        {
            return;
        }

        lock (MutationSync)
        {
            RegisterXianGuClaimUnsafe(candidate, floor);
        }
    }

    private static bool CanCandidateOwnXianGuClaim(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredCard,
        IReadOnlySet<CardModel>? ignoredCards
    )
    {
        CardModel[] conflicts = FindConflicts(
            runState,
            candidate,
            ignoredCard,
            ignoredCards
        );

        if (conflicts.Length == 0)
        {
            return true;
        }

        XianGuPriority candidatePriority =
            GetXianGuPriority(runState, candidate);

        foreach (CardModel existing in conflicts)
        {
            if (candidatePriority.CompareTo(
                    GetXianGuPriority(runState, existing)
                ) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void ReconcileConflictingXianGu(
        IRunState runState,
        CardModel winner,
        CardModel? ignoredCard,
        IReadOnlySet<CardModel>? ignoredCards
    )
    {
        foreach (
            CardModel loser in FindConflicts(
                runState,
                winner,
                ignoredCard,
                ignoredCards
            )
        )
        {
            DemoteFromXianGu(loser);
            ClaimFloorState[loser] = 0;

            Entry.Logger.Info(
                $"仙蛊唯一性仲裁：保留玩家 {winner.Owner?.NetId} 的 " +
                $"{winner.Id}，将玩家 {loser.Owner?.NetId} 的同名牌降回" +
                $"{XianGuRank - 1} 转。"
            );
        }
    }

    private static void DemoteFromXianGu(CardModel card)
    {
        if (card is AbstractGuCard gu)
        {
            gu.ReconcileGuRankForUniqueness(XianGuRank - 1);
            return;
        }

        // IsXianGu 只认具体蛊牌基类，正常流程不会走到这里；
        // 真出现说明类型体系被改坏了，宁可留下日志也不要静默破坏唯一性。
        Entry.Logger.Warn(
            $"仙蛊唯一性仲裁：{card.Id} 不是可写转数的蛊牌，无法降转。"
        );
    }

    private static void RegisterXianGuClaimUnsafe(
        CardModel card,
        int floor
    )
    {
        if (ClaimFloorState[card] == 0)
        {
            ClaimFloorState[card] = Math.Max(0, floor) + 1;
        }
    }

    private static XianGuPriority GetXianGuPriority(
        IRunState runState,
        CardModel card
    )
    {
        int savedFloor = ClaimFloorState[card];
        int claimFloor = savedFloor == 0
            ? int.MinValue
            : savedFloor - 1;

        // 尚未成为仙蛊的候选牌使用当前楼层参与本次仲裁。
        if (!IsXianGu(card))
        {
            claimFloor = runState.TotalFloor;
        }

        Player? owner = card.Owner;
        int playerSlot = owner == null
            ? int.MaxValue
            : runState.GetPlayerSlotIndex(owner);
        int deckIndex = owner == null
            ? int.MaxValue
            : FindDeckIndex(owner, card);

        return new XianGuPriority(
            claimFloor,
            playerSlot,
            deckIndex,
            owner?.NetId ?? 0UL
        );
    }

    /// <summary>
    /// 找出整局范围内与 <paramref name="candidate"/> 同名、且本身已是仙蛊的牌。
    /// 候选牌自身、显式忽略的牌与忽略集合内的牌都不计入冲突。
    /// </summary>
    private static CardModel[] FindConflicts(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredCard,
        IReadOnlySet<CardModel>? ignoredCards
    )
    {
        List<CardModel> conflicts = [];

        foreach (Player player in runState.Players)
        {
            foreach (CardModel existing in player.Deck.Cards)
            {
                if (ReferenceEquals(existing, candidate) ||
                    ReferenceEquals(existing, ignoredCard) ||
                    (ignoredCards?.Contains(existing) ?? false) ||
                    !IsXianGu(existing) ||
                    existing.Id != candidate.Id)
                {
                    continue;
                }

                conflicts.Add(existing);
            }
        }

        return conflicts.ToArray();
    }

    private static int FindDeckIndex(
        Player player,
        CardModel card
    )
    {
        IReadOnlyList<CardModel> cards = player.Deck.Cards;

        for (int index = 0; index < cards.Count; index++)
        {
            if (ReferenceEquals(cards[index], card))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    /// <summary>
    /// 仙蛊归属的确定性排序键：ClaimFloor 越小越优先（越早成仙越优先），
    /// 之后的字段只为在完全并列时给出稳定顺序。
    /// </summary>
    private readonly record struct XianGuPriority(
        int ClaimFloor,
        int PlayerSlot,
        int DeckIndex,
        ulong PlayerNetId
    ) : IComparable<XianGuPriority>
    {
        public int CompareTo(XianGuPriority other)
        {
            int result = ClaimFloor.CompareTo(other.ClaimFloor);
            if (result != 0)
            {
                return result;
            }

            result = PlayerSlot.CompareTo(other.PlayerSlot);
            if (result != 0)
            {
                return result;
            }

            result = DeckIndex.CompareTo(other.DeckIndex);
            return result != 0
                ? result
                : PlayerNetId.CompareTo(other.PlayerNetId);
        }
    }
}
