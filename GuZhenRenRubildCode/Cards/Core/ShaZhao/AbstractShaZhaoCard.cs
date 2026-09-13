using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 所有杀招牌的公共父类。
///
/// 杀招与蛊牌是两套体系：杀招不是蛊牌（不实现 <see cref="IGuCard"/>），
/// 因此它按普通卡牌规则进入手牌与弃牌堆、消耗原生能量，而不是元气。
/// 它只借用蛊牌的品阶作为"由材料决定的强度"。
///
/// 公共规则：
/// 1. 杀招只能由杀招推演在战斗中创建，不参与战斗内随机生成；
/// 2. 转数等于组方材料中的最高转数，不能被升炼提升；
/// 3. 杀招永不进入普通卡牌奖励（注册在杀招专属卡池）；
/// 4. 材料在杀招存在期间被封装进蛊封存区，不再参与补位与推演候选；
/// 5. 杀招达到生命周期终点时消耗自身，并把材料送还完整冷却。
/// </summary>
public abstract class AbstractShaZhaoCard : ModCardTemplate
{
    /// <summary>杀招在完成组方前显示为 0 转。</summary>
    public const int MinimumShaZhaoRank = 0;

    // 杀招转数由材料决定，同样需要随存档与联机状态恢复。
    private static readonly SavedAttachedState<CardModel, int> RankState = new(
        Entry.ModId + ".sha_zhao.rank",
        static () => MinimumShaZhaoRank
    );

    // 单独记录"是否已经写入过转数"，避免尚未组方的杀招被当成初始转数读取。
    private static readonly SavedAttachedState<CardModel, bool> RankAssignedState = new(
        Entry.ModId + ".sha_zhao.rank_assigned",
        static () => false
    );

    // 次数型杀招的已用次数。
    private static readonly SavedAttachedState<CardModel, int> SpentUsesState = new(
        Entry.ModId + ".sha_zhao.spent_uses",
        static () => 0
    );

    // 阶段型杀招的当前阶段。
    private static readonly SavedAttachedState<CardModel, int> StageState = new(
        Entry.ModId + ".sha_zhao.stage",
        static () => 0
    );

    // CardModel 在创建可变副本时会复制普通字段，因此本地字段用于保证克隆后的即时状态正确。
    private int _guRank = MinimumShaZhaoRank;
    private bool _rankAssigned;

    /// <summary>杀招允许达到的最高转数。默认九转。</summary>
    public virtual int MaxGuRank => 9;

    /// <summary>当前杀招转数，等于组方材料中的最高转数。</summary>
    public int GuRank
    {
        get
        {
            int rank = RankAssignedState[this] ? RankState[this] : _guRank;
            _guRank = Math.Clamp(rank, MinimumShaZhaoRank, MaxGuRank);
            return _guRank;
        }
        private set
        {
            _guRank = Math.Clamp(value, MinimumShaZhaoRank, MaxGuRank);
            RankState[this] = _guRank;
        }
    }

    // 杀招固定属于杀招专属卡池：配方注册表按该池扫描，普通奖励也不会碰到它。
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenRubildShaZhaoCardPool>();

    // 杀招只能由推演流程创建，不参与战斗内的随机生成。
    public override bool CanBeGeneratedInCombat => false;

    // 杀招不参与原生升级；转数只由材料决定。
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 创建一张尚未完成组方的杀招规范模型。
    ///
    /// 初始转数为零；真正通过推演生成时，会由
    /// <see cref="InitializeFromMaterials"/> 写入最终转数。
    /// </summary>
    protected AbstractShaZhaoCard(
        int baseCost,
        CardType type,
        TargetType target,
        bool showInCardLibrary = true
    ) : base(
        baseCost,
        type,
        CardRarity.Rare,
        target,
        showInCardLibrary
    )
    {
        // 写入 0 转同时标记"已初始化"，使规范模型与预览都不会被当成待抽取的奖励蛊牌。
        SetGuRank(MinimumShaZhaoRank);
    }

    // =================================================================
    //  转数
    // =================================================================

    /// <summary>
    /// 获取一张材料牌参与杀招组方时的转数。
    /// 非蛊牌按零转处理。
    /// </summary>
    public static int GetMaterialRank(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuCard gu ? Math.Max(0, gu.GuRank) : 0;
    }

    /// <summary>
    /// 使用玩家选定的材料组方。
    ///
    /// 最终转数等于所有材料转数的最大值；材料快照按类型名与转数稳定排序，
    /// 使不同客户端拿到相同顺序，便于具体杀招实现与顺序相关的效果。
    /// </summary>
    internal void InitializeFromMaterials(
        IReadOnlyList<CardModel> orderedMaterials
    )
    {
        ArgumentNullException.ThrowIfNull(orderedMaterials);

        if (orderedMaterials.Count == 0)
        {
            throw new ArgumentException(
                "杀招至少需要一张材料牌。",
                nameof(orderedMaterials)
            );
        }

        CardModel[] materials = orderedMaterials
            .Select((card, index) => (card, index))
            .OrderBy(
                item => item.card.GetType().FullName ??
                    item.card.GetType().Name,
                StringComparer.Ordinal
            )
            .ThenByDescending(item => GetMaterialRank(item.card))
            .ThenBy(item => item.index)
            .Select(item => item.card)
            .ToArray();

        SetGuRank(
            materials
                .Select(GetMaterialRank)
                .DefaultIfEmpty(MinimumShaZhaoRank)
                .Max()
        );

        OnShaZhaoComposed(materials);
    }

    /// <summary>
    /// 统一的转数写入口。杀招允许 0 转，因此下限与蛊牌的一转不同。
    /// </summary>
    protected void SetGuRank(int amount)
    {
        GuRank = Math.Max(MinimumShaZhaoRank, amount);
        _rankAssigned = true;
        RankAssignedState[this] = true;
        OnGuRankChanged();
    }

    /// <summary>转数改变或状态恢复后的刷新钩子，子类据此重算派生数值。</summary>
    protected virtual void OnGuRankChanged()
    {
    }

    /// <summary>
    /// 杀招组方完成后的扩展钩子。具体杀招可以读取有序材料，
    /// 为不同材料组合实现不同效果。
    /// </summary>
    protected virtual void OnShaZhaoComposed(
        IReadOnlyList<CardModel> orderedMaterials
    )
    {
    }

    /// <summary>读档或复制后的扩展钩子。</summary>
    protected virtual void OnShaZhaoStateLoaded()
    {
    }

    // 存档反序列化完成后立即恢复本地缓存，避免显示值与持久化转数不一致。
    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();

        _guRank = GuRank;
        _rankAssigned = RankAssignedState[this] || _rankAssigned;

        if (_rankAssigned && !RankAssignedState[this])
        {
            RankState[this] = _guRank;
            RankAssignedState[this] = true;
        }

        OnGuRankChanged();
        OnShaZhaoStateLoaded();
    }

    // 把杀招转数与生命周期进度注入本地化参数，供卡面描述直接使用。
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Rank", GuRank);
        description.Add("MaxGuRank", MaxGuRank);
        description.Add("ShaZhaoMaxUses", EffectiveMaxUses);
        description.Add("ShaZhaoRemainingUses", EffectiveMaxUses - SpentUses);
        description.Add("ShaZhaoMaxStages", EffectiveMaxStages);
        description.Add("ShaZhaoStage", CurrentStage + 1);
        description.Add("MaterialsBound", BoundMaterialCount);
    }

    // =================================================================
    //  生命周期
    // =================================================================

    /// <summary>
    /// 杀招生命周期类型，决定材料蛊的返还时机。
    /// </summary>
    public enum ShaZhaoLifecycle
    {
        /// <summary>瞬发：使用一次后消耗，材料立即进入完整冷却。</summary>
        Instant,

        /// <summary>次数型：可连续使用多次，用尽后消耗并返还材料。</summary>
        Charged,

        /// <summary>阶段型：按阶段推进形态，最终阶段后消耗并返还材料。</summary>
        Staged,

        /// <summary>封印型：效果真正完成后消耗，并解除材料封装。</summary>
        Sealed,
    }

    /// <summary>本杀招的生命周期类型，具体杀招按需重写。</summary>
    public virtual ShaZhaoLifecycle Lifecycle => ShaZhaoLifecycle.Instant;

    /// <summary>
    /// 推演时是否把配方材料永久封存（不参与后续返还）。
    ///
    /// 默认 false：材料按生命周期在杀招消耗时归还。
    /// 重写为 true 的杀招在消耗时只解除绑定记录，材料保持封存状态。
    /// </summary>
    public virtual bool MaterialsSealedPermanently => false;

    /// <summary>次数型杀招的总使用次数。</summary>
    public virtual int ShaZhaoMaxUses => 1;

    /// <summary>阶段型杀招的总阶段数。</summary>
    public virtual int MaxStages => 1;

    /// <summary>已使用次数（次数型）。</summary>
    public int SpentUses
    {
        get => Math.Clamp(SpentUsesState[this], 0, EffectiveMaxUses);
        private set => SpentUsesState[this] = value;
    }

    /// <summary>当前阶段（阶段型，从 0 开始）。</summary>
    public int CurrentStage
    {
        get => Math.Clamp(StageState[this], 0, EffectiveMaxStages);
        private set => StageState[this] = value;
    }

    /// <summary>防止具体杀招把总次数或总阶段数写成非正数。</summary>
    private int EffectiveMaxUses => Math.Max(1, ShaZhaoMaxUses);

    private int EffectiveMaxStages => Math.Max(1, MaxStages);

    // =================================================================
    //  材料封装
    // =================================================================

    /// <summary>是否仍绑定材料（杀招存在期间材料被封装）。</summary>
    public bool HasBoundMaterials => BoundMaterialCount > 0;

    /// <summary>仍被本杀招封装的材料蛊数量。</summary>
    public int BoundMaterialCount =>
        ShaZhaoBindingService.GetBoundMaterialCount(this);

    /// <summary>仍被本杀招封装的真实材料卡牌实例。</summary>
    public IReadOnlyList<CardModel> BoundMaterials =>
        ShaZhaoBindingService.GetBoundMaterials(this);

    /// <summary>
    /// 创建成功后把真实材料封装进蛊封存区，并记录绑定关系。
    /// </summary>
    internal async Task BindMaterialsAsync(
        IReadOnlyList<CardModel> materials
    )
    {
        ArgumentNullException.ThrowIfNull(materials);

        if (materials.Count == 0)
        {
            return;
        }

        using (ShaZhaoSynthesisScope.Enter())
        {
            foreach (CardModel material in materials)
            {
                await ShaZhaoBindingService.MarkMaterialSealedAsync(
                    material,
                    this
                );
            }
        }
    }

    /// <summary>
    /// 杀招生命周期推进：次数型扣次、阶段型推进；
    /// 达到终态时消耗杀招并返还材料。
    /// 具体杀招需要在各自 OnPlay 末尾调用。
    /// </summary>
    protected async Task AdvanceLifecycleAsync(
        PlayerChoiceContext choiceContext
    )
    {
        switch (Lifecycle)
        {
            case ShaZhaoLifecycle.Charged:
                int spent = SpentUses + 1;
                SpentUses = spent;

                if (spent < EffectiveMaxUses)
                {
                    return;
                }

                break;

            case ShaZhaoLifecycle.Staged:
                int nextStage = CurrentStage + 1;
                CurrentStage = nextStage;

                if (nextStage < EffectiveMaxStages)
                {
                    return;
                }

                break;

            case ShaZhaoLifecycle.Instant:
            case ShaZhaoLifecycle.Sealed:
            default:
                // 瞬发与封印型都在本次结算后直接收口。
                break;
        }

        await ConsumeAndReturnAsync(choiceContext);
    }

    /// <summary>
    /// 消耗本杀招并把材料送还恢复流程。
    /// </summary>
    protected async Task ConsumeAndReturnAsync(
        PlayerChoiceContext choiceContext
    )
    {
        Player player = Owner;
        await ShaZhaoBindingService.FinalizeAsync(
            this,
            player,
            ShaZhaoBindingService.FinalizeReason.Completed
        );
        await CardCmd.Exhaust(
            choiceContext,
            this,
            causedByEthereal: false,
            skipVisuals: false
        );
    }

    /// <summary>
    /// 主动解体（右键）：1 费，材料返回并额外增加 1 回合恢复。
    /// </summary>
    internal async Task<bool> TryDismantleAsync(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        if (!HasBoundMaterials ||
            player.PlayerCombatState is not { } combatState ||
            combatState.Energy < 1m)
        {
            return false;
        }

        await PlayerCmd.LoseEnergy(1, player);
        await ShaZhaoBindingService.FinalizeAsync(
            this,
            player,
            ShaZhaoBindingService.FinalizeReason.Dismantled
        );
        await CardCmd.Exhaust(
            choiceContext,
            this,
            causedByEthereal: false,
            skipVisuals: false
        );
        return true;
    }
}
