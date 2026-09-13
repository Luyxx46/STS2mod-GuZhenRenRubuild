using System.Runtime.CompilerServices;
using GuZhenRenRubild.Cards.Core.Abstractions;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.CardPiles;

namespace GuZhenRenRubild.Cards.Core.Runtime;

/// <summary>
/// 管理战斗内蛊牌的专用牌堆，并实现完整循环：储备区 → 激活区 → 恢复区 → 储备区。
/// 激活区以额外手牌形式展示，可直接打出；储备区保存等待补位的蛊牌；恢复区保存使用次数耗尽、尚未恢复的蛊牌；
/// 封存区保存被杀招封装、暂时不参与循环的材料蛊。
/// </summary>
public static class GuCardPileSystem
{
    // 激活区最多同时展示的蛊牌数量，空位会从储备区按顺序自动补充。
    public const int ActiveCapacity = 5;

    // 四个本地标识会与模组编号组合成全局唯一牌堆编号。
    private const string ActiveLocalId = "gu_active";
    private const string StorageLocalId = "gu_storage";
    private const string RecoveryLocalId = "gu_recovery";
    // 蛊封存区：杀招材料在杀招存在期间被封存在这里，不再参与推演候选与补位。
    private const string SealedLocalId = "gu_sealed";
    // 牌堆图标沿用旧模组 STS2_GuZhenRen（GuZhenRenPersonal）的真实美术，
    // 原名分别是「蛊存放排队」与「蛊冷却排队」，此处只把资源根目录换成当前模组。
    private const string StorageIconPath =
        $"res://{Entry.ModId}/images/ui/GuChunFangPaiDui.png";
    private const string RecoveryIconPath =
        $"res://{Entry.ModId}/images/ui/GuLengQuePaiDui.png";
    // 封存区图标沿用旧模组 STS2_GuZhenRen 的蛊封存堆 SVG（本仓库内为 materials/GuPile.svg）。
    // 使用独立 SVG 而不是复用冷却堆 PNG，避免封存蛊与冷却蛊在界面上无法区分。
    private const string SealedIconPath =
        $"res://{Entry.ModId}/materials/GuPile.svg";

    // 注册过程使用互斥锁和初始化标记，避免多入口重复注册同名牌堆。
    private static readonly object SyncRoot = new();
    private static readonly ConditionalWeakTable<Player, OpeningEntryState>
        OpeningStates = new();
    private static bool _initialized;

    private sealed class OpeningEntryState(CardModel[] cards)
    {
        internal CardModel[] Cards { get; } = cards;
        internal Task? EntryTask { get; set; }
        internal bool Completed { get; set; }
    }

    // 注册完成后缓存 RitsuLib 返回的牌堆类型，后续所有移动操作都使用这些类型。
    public static PileType ActivePileType { get; private set; }
    public static PileType StoragePileType { get; private set; }
    public static PileType RecoveryPileType { get; private set; }

    /// <summary>
    /// 蛊封存区：杀招材料在杀招存在期间被封存在这里。
    /// 牌堆只描述位置，"这张牌属于哪一张杀招"由
    /// <c>ShaZhaoBindingService</c> 在材料侧记录的绑定字符串描述，
    /// 两者必须一起使用才能安全地返还材料。
    /// </summary>
    public static PileType SealedPileType { get; private set; }

    // 向 RitsuLib 注册四个仅在战斗期间存在的蛊牌牌堆及其界面表现。
    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            ModCardPileRegistry registry = ModCardPileRegistry.For(Entry.ModId);

            // 激活区使用额外手牌样式，允许直接打出，并显示原生“可打出”高亮。
            ActivePileType = registry.RegisterOwned(
                ActiveLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.ExtraHand,
                    CardShouldBeVisible = true,
                    ExtraHand = new ModCardPileExtraHandSpec
                    {
                        AllowCardPlay = true,
                        ShowPlayableGlow = true,
                    },
                }
            ).PileType;

            // 储备区显示在左下角主位置，用于保存尚未进入激活区的蛊牌。
            StoragePileType = registry.RegisterOwned(
                StorageLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomLeft,
                    IconPath = StorageIconPath,
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftPrimary,
                        Vector2.Zero
                    ),
                    CardShouldBeVisible = true,
                }
            ).PileType;

            // 恢复区位于储备区旁边，用于保存等待恢复倒计时结束的蛊牌。
            RecoveryPileType = registry.RegisterOwned(
                RecoveryLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomLeft,
                    IconPath = RecoveryIconPath,
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftSecondary,
                        new Vector2(-200f, 0f)
                    ),
                    CardShouldBeVisible = true,
                }
            ).PileType;

            // 封存区使用右下角自动槽位，紧邻原版消耗牌堆；杀招材料在被封装期间
            // 只从界面上可见，不参与补位、恢复与推演候选。
            SealedPileType = registry.RegisterOwned(
                SealedLocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomRight,
                    IconPath = ResourceLoader.Exists(SealedIconPath)
                        ? SealedIconPath
                        : RecoveryIconPath,
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomRightPrimary,
                        new Vector2(100f, -140f)
                    ),
                    HoverTipPlacement =
                        ModCardPileHoverTipPlacement.AboveButtonCentered,
                    CardShouldBeVisible = true,
                }
            ).PileType;

            _initialized = true;
        }
    }

    public static void Uninitialize()
    {
        // RitsuLib 的牌堆注册在整个进程内长期有效，当前没有安全的反注册接口。
        // 因此卸载阶段不清空初始化标记，避免随后再次初始化时重复注册相同编号。
    }

    // 战斗开始时只把真正的蛊牌收进储备区；伴生牌继续留在原生普通抽牌体系。
    // 开场前 5 张蛊在原生首次 DrawInternal 完成后再逐张进入额外手牌。
    internal static void InitializeGuCardsForCombat(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();

        CardPile active = ActivePileType.GetPile(owner);
        CardPile storage = StoragePileType.GetPile(owner);
        CardPile recovery = RecoveryPileType.GetPile(owner);
        CardPile sealedPile = SealedPileType.GetPile(owner);
        CardPile[] allPiles =
        [
            PileType.Draw.GetPile(owner),
            PileType.Discard.GetPile(owner),
            PileType.Hand.GetPile(owner),
            active,
            storage,
            recovery,
            sealedPile,
        ];

        CardModel[] guCards = allPiles
            .SelectMany(static pile => pile.Cards)
            .Where(static card => card is IGuCard)
            .Distinct()
            .ToArray();

        HashSet<CardPile> changed = [];
        foreach (CardModel card in guCards)
        {
            GuCardRuntime.ResetForCombat(card);
            MoveWithoutNotification(card, storage, changed);
        }

        foreach (CardPile pile in changed)
        {
            pile.InvokeContentsChanged();
        }

        CardModel[] openingCards = storage.Cards
            .Where(static card => card is IGuCard)
            .Take(ActiveCapacity)
            .ToArray();
        OpeningStates.Remove(owner);
        OpeningStates.Add(owner, new OpeningEntryState(openingCards)
        {
            Completed = openingCards.Length == 0,
        });
    }

    internal static Task? BeginOpeningGuEntry(Player owner, bool fromHandDraw)
    {
        if (!fromHandDraw || owner.PlayerCombatState?.TurnNumber != 1)
        {
            return null;
        }

        if (!OpeningStates.TryGetValue(owner, out OpeningEntryState? state) ||
            state.Completed)
        {
            return state?.EntryTask;
        }

        state.EntryTask ??= RunOpeningEntryAsync(owner, state);
        return state.EntryTask;
    }

    private static async Task RunOpeningEntryAsync(
        Player owner,
        OpeningEntryState state
    )
    {
        try
        {
            CardPile storage = StoragePileType.GetPile(owner);
            CardPile active = ActivePileType.GetPile(owner);
            foreach (CardModel card in state.Cards)
            {
                if (active.Cards.Count >= ActiveCapacity ||
                    card.Pile?.Type != StoragePileType)
                {
                    continue;
                }

                CardPileAddResult result = await CardPileCmd.Add(
                    card,
                    ActivePileType,
                    CardPilePosition.Bottom,
                    clonedBy: null,
                    skipVisuals: false
                );
                if (!result.success)
                {
                    Entry.Logger.Warn($"Could not enter opening Gu card {card.Id}.");
                    break;
                }
            }
        }
        finally
        {
            state.Completed = true;
        }
    }

    // 把额外手牌中的蛊牌移入原生手牌区，供原生打牌校验和联机流程继续处理。
    internal static void MoveToNativeHand(CardModel card)
    {
        MoveCardWithoutAnimation(card, PileType.Hand.GetPile(card.Owner));
    }

    // 通过原生牌堆命令异步移动卡牌；如果目标牌堆与当前位置相同则直接返回，保证操作幂等。
    internal static async Task MoveCardToPileAsync(
        CardModel card,
        PileType targetPile,
        bool skipVisuals
    )
    {
        if (card.Pile?.Type == targetPile)
        {
            return;
        }

        await CardPileCmd.Add(
            card,
            targetPile,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: skipVisuals
        );
    }

    // 在回合刷新时检查恢复区，将达到恢复回合的蛊牌重置使用次数并送回储备区，随后补充激活区。
    public static async Task RestoreRecoveredCardsAsync(
        Player owner,
        int turnNumber
    )
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();

        CardModel[] recovered = RecoveryPileType
            .GetPile(owner)
            .Cards
            .Where(card => GuCardRuntime.IsRecoveryReady(card, turnNumber))
            .ToArray();

        foreach (CardModel card in recovered)
        {
            GuCardRuntime.CompleteRecovery(card);
            await MoveCardToPileAsync(card, StoragePileType, skipVisuals: false);
        }

        await RefillActiveAsync(owner, skipVisuals: false);
    }

    // 持续从储备区首部向激活区补牌，直到激活区满、储备区为空或底层移动命令失败。
    public static async Task RefillActiveAsync(Player owner, bool skipVisuals)
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();

        CardPile active = ActivePileType.GetPile(owner);
        CardPile storage = StoragePileType.GetPile(owner);

        while (active.Cards.Count < ActiveCapacity && storage.Cards.Count > 0)
        {
            CardModel next = storage.Cards.First();
            CardPileAddResult result = await CardPileCmd.Add(
                next,
                ActivePileType,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: skipVisuals
            );

            // 失败后立即停止循环，避免同一张无法移动的牌导致无限重试。
            if (!result.success)
            {
                Entry.Logger.Warn($"Could not refill Gu card {next.Id}.");
                break;
            }
        }
    }

    // 用内部牌堆接口执行无动画移动，并在完成后统一触发内容变更事件。
    private static void MoveCardWithoutAnimation(CardModel card, CardPile target)
    {
        // 批量移动时先静默修改牌堆内容，最后只对真正变化过的牌堆各发送一次变更通知。
        HashSet<CardPile> changed = [];
        MoveWithoutNotification(card, target, changed);
        foreach (CardPile pile in changed)
        {
            pile.InvokeContentsChanged();
        }
    }

    // 最底层的静默移动：不播放动画、不立即广播事件，只把变化过的源牌堆和目标牌堆记录到集合中。
    private static void MoveWithoutNotification(
        CardModel card,
        CardPile target,
        HashSet<CardPile> changed
    )
    {
        CardPile? source = card.Pile;
        if (ReferenceEquals(source, target))
        {
            return;
        }

        source?.RemoveInternal(card, silent: true);
        target.AddInternal(card, silent: true);
        if (source != null)
        {
            changed.Add(source);
        }
        changed.Add(target);
    }

    // 所有战斗入口都允许惰性初始化，避免调用顺序变化时出现未注册牌堆。
    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
