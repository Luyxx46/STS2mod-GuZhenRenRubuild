using GuZhenRenRubild.Cards.Core.Runtime;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 杀招材料绑定的唯一领域服务。
///
/// 负责封存、解绑与返还，以及异常移出和战斗结束的兜底；
/// 推演选择与费用结算不在此处处理。
///
/// 绑定关系分两侧记录：
/// 1. 每张材料蛊记录"被哪一张杀招封装"（用于反查与防重复封装）；
/// 2. 每张杀招记录"封装了几张材料"（用于低成本判断是否仍有绑定）。
/// 两侧都随存档与联机快照一起恢复；绑定只在一个战斗内有效。
/// </summary>
internal static class ShaZhaoBindingService
{
    private static readonly SavedAttachedState<CardModel, string> BoundShaZhaoState = new(
        Entry.ModId + ".sha_zhao.material_bound",
        static () => string.Empty
    );

    private static readonly SavedAttachedState<CardModel, int> BoundMaterialCountState = new(
        Entry.ModId + ".sha_zhao.bound_materials",
        static () => 0
    );

    // 绑定键在首次封装时冻结并随存档保存：战斗卡编号的可用性可能随牌堆状态变化
    // （封装时取不到、收口时又取得到），如果每次现算，绑定键就会漂移，
    // 导致材料在收口时反查不到、永远留在封存区。
    private static readonly SavedAttachedState<CardModel, string> BindingTokenState = new(
        Entry.ModId + ".sha_zhao.binding_token",
        static () => string.Empty
    );

    internal enum FinalizeReason
    {
        /// <summary>正常用完。</summary>
        Completed,

        /// <summary>杀招被其他效果异常移出战斗。</summary>
        AbnormalRemoval,

        /// <summary>战斗结束兜底。</summary>
        CombatEnd,
    }

    internal static int GetBoundMaterialCount(AbstractShaZhaoCard shaZhao)
    {
        ArgumentNullException.ThrowIfNull(shaZhao);

        return Math.Max(0, BoundMaterialCountState[shaZhao]);
    }

    /// <summary>
    /// 判断一张蛊牌身上是否还带着"被封装的杀招材料"记录。
    /// 推演候选与战斗结束清场都要用它，避免已经绑定但移动失败的蛊牌被再次选中。
    /// </summary>
    internal static bool HasMaterialBinding(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return BoundShaZhaoState[card].Length > 0;
    }

    /// <summary>
    /// 取出（必要时创建并冻结）一张杀招在绑定记录中使用的键。
    ///
    /// 必须是**战斗实例**身份而不是模型身份：同名杀招可以在同一场战斗里同时存在
    /// （八转起每场最多推演两次，且推演牌本身也可能有多张），若用模型编号作键，
    /// 收口其中一张会连带清掉另一张的材料绑定。
    /// <c>NetCombatCardDb</c> 的战斗卡编号正是每场战斗内唯一的实例身份，
    /// 因此优先使用它；取不到编号时才退回模型编号（此时同名杀招之间不保证可区分）。
    ///
    /// 键一旦生成就写入附加状态并复用，保证封装与收口两个时刻看到同一个键。
    /// </summary>
    private static string GetOrCreateBindingToken(AbstractShaZhaoCard shaZhao)
    {
        string existing = BindingTokenState[shaZhao];

        if (existing.Length > 0)
        {
            return existing;
        }

        string token = NetCombatCardDb.Instance.TryGetCardId(
            shaZhao,
            out uint networkId
        )
            ? "net:" + networkId.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            )
            : "model:" + shaZhao.Id;

        BindingTokenState[shaZhao] = token;
        return token;
    }

    /// <summary>
    /// 解析当前仍被指定杀招封装的材料牌。
    /// 以材料侧的绑定记录为准，因此即使杀招实例被克隆或重建也能正确反查。
    /// </summary>
    internal static IReadOnlyList<CardModel> GetBoundMaterials(
        AbstractShaZhaoCard shaZhao
    )
    {
        ArgumentNullException.ThrowIfNull(shaZhao);

        if (GetBoundMaterialCount(shaZhao) <= 0)
        {
            return Array.Empty<CardModel>();
        }

        string token = BindingTokenState[shaZhao];

        if (token.Length == 0)
        {
            return Array.Empty<CardModel>();
        }

        return shaZhao.Owner?.PlayerCombatState?.AllCards
            .Where(card => string.Equals(
                BoundShaZhaoState[card],
                token,
                StringComparison.Ordinal
            ))
            .ToArray()
            ?? [];
    }

    /// <summary>
    /// 战斗开始时清掉上一场战斗可能残留的材料绑定。
    ///
    /// 正常路径由遗物的 <c>AfterCombatEnd</c> 兜底清理；这里再收一次口，
    /// 使"钩子没有触发、或材料当时不在战斗状态里"导致的残留不会跨战斗生效。
    /// 这一点尤其重要：战斗卡编号会在新战斗里重新分配，残留的编号可能
    /// 恰好被新战斗中的另一张牌占用，从而把无关蛊牌误判为某张杀招的材料。
    /// </summary>
    internal static void ClearStaleBindings(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        // 牌组里的永久牌一定要扫：材料本来就是牌组里的蛊牌，
        // 而战斗状态在 BeforeCombatStart 时未必已经把牌组分发完毕。
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            if (HasMaterialBinding(card))
            {
                ClearMaterialBinding(card);
            }
        }

        if (player.PlayerCombatState is not { } combatState)
        {
            return;
        }

        // 再扫一遍战斗内卡牌，覆盖战斗内创建的副本。
        foreach (CardModel card in combatState.AllCards.ToArray())
        {
            if (HasMaterialBinding(card))
            {
                ClearMaterialBinding(card);
            }
        }
    }

    /// <summary>
    /// 把一张材料蛊封装进蛊封存区。
    /// 同一张蛊牌不能同时作为两张杀招的材料。
    /// </summary>
    internal static async Task MarkMaterialSealedAsync(
        CardModel material,
        AbstractShaZhaoCard shaZhao
    )
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(shaZhao);

        if (BoundShaZhaoState[material].Length > 0)
        {
            // 正常路径下不会发生：材料一旦被封存就会离开蛊手牌与蛊待命堆，
            // 不可能在同一场战斗里被第二张杀招再次选中。残留绑定只可能来自
            // 被异常打断的返还流程，因此这里以最后一次封装为准并记录警告，
            // 而不是抛出异常让整次推演失败。
            Entry.Logger.Warn(
                $"蛊牌 {material.Id} 仍带着上一次封装留下的绑定记录，已按本次封装覆盖。"
            );
        }

        if (material.Owner is not { } player)
        {
            throw new InvalidOperationException(
                $"蛊牌 {material.Id} 没有归属玩家，无法封装为杀招材料。"
            );
        }

        string token = GetOrCreateBindingToken(shaZhao);
        BoundShaZhaoState[material] = token;
        BoundMaterialCountState[shaZhao] = GetBoundMaterialCount(shaZhao) + 1;

        CardPile sealedPile = GuCardPileSystem.SealedPileType.GetPile(player);

        if (!ReferenceEquals(material.Pile, sealedPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.SealedPileType,
                skipVisuals: false
            );
        }

        if (material.Pile?.Type != GuCardPileSystem.SealedPileType)
        {
            // 移动没有生效：撤销绑定记录，避免杀招留下"计数大于零却找不到材料"
            // 的状态（那会让后续的收口与返还对着不存在的材料空转）。
            ClearMaterialBinding(material);
            BoundMaterialCountState[shaZhao] = Math.Max(
                0,
                GetBoundMaterialCount(shaZhao) - 1
            );
            Entry.Logger.Warn(
                $"蛊牌 {material.Id} 未能进入蛊封存区，已撤销本次封装记录。"
            );
        }
    }

    /// <summary>
    /// 收口一次杀招绑定。
    ///
    /// 正常用完或异常移出会让材料从零开始完整冷却并额外延后一回合；
    /// 战斗结束只清理战斗期绑定记录，材料留在原地交给游戏的战斗牌堆回收流程。
    ///
    /// 战斗结束时即使材料是"永久封存"也必须解绑，否则绑定状态会跨战斗残留，
    /// 使材料在下一场战斗中被误判为"已被其他杀招封装"。
    /// </summary>
    internal static async Task FinalizeAsync(
        AbstractShaZhaoCard shaZhao,
        Player player,
        FinalizeReason reason
    )
    {
        ArgumentNullException.ThrowIfNull(shaZhao);
        ArgumentNullException.ThrowIfNull(player);

        if (GetBoundMaterialCount(shaZhao) <= 0)
        {
            return;
        }

        IReadOnlyList<CardModel> materials = GetBoundMaterials(shaZhao);

        if (reason == FinalizeReason.CombatEnd)
        {
            foreach (CardModel material in materials)
            {
                ClearMaterialBinding(material);
            }

            ClearShaZhaoBinding(shaZhao);
            return;
        }

        if (shaZhao.MaterialsSealedPermanently)
        {
            // 永久封存的材料不返还，但仍要清掉绑定记录，
            // 否则材料会在下一场战斗里被判定为已被封装。
            foreach (CardModel material in materials)
            {
                ClearMaterialBinding(material);
            }

            ClearShaZhaoBinding(shaZhao);
            return;
        }

        foreach (CardModel material in materials)
        {
            await ReturnMaterialAsync(material, player);
        }

        ClearShaZhaoBinding(shaZhao);
    }

    /// <summary>
    /// 战斗结束兜底：清理该玩家全部杀招材料绑定。
    /// 由遗物的 AfterCombatEnd 时机调用，不需要额外 Harmony 补丁。
    ///
    /// 清理以**材料侧的绑定记录**为准（而不是杀招侧的计数）：只要战斗内还有
    /// 任何卡牌带着指向某张杀招的绑定字符串就一律清掉。这样即使中间某条
    /// 返还路径被异常打断、两侧记录已经失配，绑定状态也不会跨战斗残留，
    /// 导致材料在下一场战斗里被误判为"已被其他杀招封装"。
    /// </summary>
    internal static void FinalizeAllForCombatEnd(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (player.PlayerCombatState is not { } combatState)
        {
            return;
        }

        CardModel[] allCards = combatState.AllCards.ToArray();

        foreach (CardModel card in allCards)
        {
            if (BoundShaZhaoState[card].Length > 0)
            {
                ClearMaterialBinding(card);
            }
        }

        foreach (AbstractShaZhaoCard shaZhao in allCards
            .OfType<AbstractShaZhaoCard>())
        {
            ClearShaZhaoBinding(shaZhao);
        }
    }

    /// <summary>
    /// 把材料送进恢复区并重新开始完整冷却。
    /// </summary>
    private static async Task ReturnMaterialAsync(
        CardModel material,
        Player player
    )
    {
        ClearMaterialBinding(material);

        if (player.PlayerCombatState is null)
        {
            // 战斗已经结束或战斗牌堆正在拆除：只解绑，不再尝试移动牌。
            // 此时材料会随战斗牌堆回收流程回到牌组，不需要额外冷却标记。
            return;
        }

        CardPile recoveryPile = GuCardPileSystem.RecoveryPileType.GetPile(player);

        if (!ReferenceEquals(material.Pile, recoveryPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.RecoveryPileType,
                skipVisuals: false
            );
        }

        // 材料从零开始完整冷却，并额外延后一回合，作为"被封存过"的代价。
        int currentTurn = player.PlayerCombatState?.TurnNumber ?? 0;
        GuCardRuntime.BeginRecovery(material, currentTurn, extraTurns: 1);
    }

    private static void ClearMaterialBinding(CardModel material)
    {
        BoundShaZhaoState[material] = string.Empty;
    }

    private static void ClearShaZhaoBinding(AbstractShaZhaoCard shaZhao)
    {
        BoundMaterialCountState[shaZhao] = 0;
        // 绑定键与绑定计数同生命周期；不清掉它会让下一次封装复用上一轮的键，
        // 而那张键对应的战斗卡编号可能已经被别的牌占用。
        BindingTokenState[shaZhao] = string.Empty;
    }
}
