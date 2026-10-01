using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Combat;
using GuZhenRenRubild.Common.Text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using GuZhenRenRubild.Cards.Core.Runtime;

namespace GuZhenRenRubild.Cards.Core.Abstractions;

/// <summary>
/// 蛊牌的统一抽象基类，负责保存蛊牌品阶，并定义蛊牌从专用激活区使用时需要遵守的基础规则。
/// 具体蛊牌只需要提供自身效果与数值成长，品阶持久化、元气消耗、使用次数和恢复流程由公共系统统一处理。
/// </summary>
public abstract class AbstractGuCard : ModCardTemplate, IGuCard
{
    // 所有蛊牌允许的最低品阶。品阶读取和写入都会被限制在合法区间内。
    public const int MinimumGuRank = 1;

    // 保存每张卡牌的实际品阶，使其能够随存档和联机状态一起恢复。
    private static readonly SavedAttachedState<CardModel, int> RankState = new(
        Entry.ModId + ".gu_rank",
        static () => MinimumGuRank
    );

    // 单独记录“是否已经完成初始品阶抽取”，避免奖励界面重复刷新时再次随机。
    private static readonly SavedAttachedState<CardModel, bool> RankAssignedState = new(
        Entry.ModId + ".gu_rank_assigned",
        static () => false
    );

    // CardModel 在创建可变副本时会复制普通字段，因此本地字段用于保证克隆后的即时状态正确。
    // SavedAttachedState 负责存档与联机同步；两套状态同时保留，可以覆盖“对象克隆”和“持久化恢复”两条数据路径。
    private int _guRank = MinimumGuRank;
    private bool _rankAssigned;

    // 当前蛊牌品阶。优先读取持久化状态，并始终限制在最低品阶与该蛊牌最大品阶之间。
    public int GuRank
    {
        get
        {
            int rank = RankAssignedState[this] ? RankState[this] : _guRank;
            _guRank = Math.Clamp(rank, MinimumGuRank, MaxGuRank);
            return _guRank;
        }
        private set
        {
            _guRank = Math.Clamp(value, MinimumGuRank, MaxGuRank);
            RankState[this] = _guRank;
        }
    }

    // 以下属性定义一张蛊牌的公共规则；具体蛊牌可按需覆盖。
    // MaxGuRank：最高品阶；MaxUses：一次激活周期内可使用次数；YuanQiCost：每次打出需要的元气；RecoveryDelayTurns：耗尽后恢复所需回合数。
    public virtual int MaxGuRank => 9;
    public virtual int MaxUses => 1;
    public virtual int YuanQiCost => 1;
    public virtual int RecoveryDelayTurns => 2;

    // 仅当本地字段和持久化字段都未标记时，才允许为奖励中的新蛊牌分配初始品阶。
    internal bool NeedsInitialRankAssignment =>
        !(_rankAssigned || RankAssignedState[this]);

    // 蛊牌不通过战斗中的普通随机生成机制产生，只由角色卡池、奖励等受控入口创建。
    public override bool CanBeGeneratedInCombat => false;

    // 原生升级资格（裁决 D-01）：事件与原版升级效果可以把"非仙蛊且未到上限"的蛊牌升 1 转。
    // 实际升转由 GuVanillaUpgradePatch 拦截 UpgradeInternal 完成（映射为 TryIncreaseGuRank），
    // CurrentUpgradeLevel 保持 0（IsUpgregated 恒 false，降级类效果永不选中蛊牌）。
    // 仙蛊（六转及以上）与已到 MaxGuRank 的蛊牌返回 0："全部升级"类效果（如 Apotheosis）
    // 靠 IsUpgradable 过滤自动排除它们。篝火「升炼」不受本属性影响（读 GuRankUpRules）。
    public override int MaxUpgradeLevel =>
        GuRank < MaxGuRank && GuRank < GuXianGuRules.XianGuRank ? 1 : 0;

    // 卡牌费用位置显示元气图标，而不是角色的普通能量图标。
    public override string? CustomEnergyIconPath => YuanQiSystem.LargeIconPath;

    // 所有蛊牌归属本角色的专属卡池。
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenRubildGuCardPool>();

    // 是否可打出由蛊牌运行时统一判断：必须位于激活区、有剩余使用次数、处于战斗中且元气足够。
    protected override bool IsPlayable => GuCardRuntime.CanActivate(this);

    // 构造时把元气费用写入 RitsuLib 的副资源费用表，使原生卡牌流程能够自动检查并扣除元气。
    protected AbstractGuCard(
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    ) : base(0, type, rarity, target, showInCardLibrary)
    {
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    // 在原生可打出判定之外，再校验蛊牌是否还有可用次数；自动打出同样遵守此限制。
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return base.ShouldPlay(card, autoPlayType) &&
            (!ReferenceEquals(card, this) || GuCardRuntime.CanUse(this));
    }

    // 蛊牌打出后的目标牌堆由运行时状态决定：仍有次数则回激活区，次数耗尽则进入恢复区。
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location
    )
    {
        if (ReferenceEquals(card, this))
        {
            location.pileType = GuCardRuntime.GetResultPile(this);
        }

        return location;
    }

    // 将品阶、最大次数、剩余次数和恢复回合数注入本地化参数，供卡牌描述中的占位符直接使用。
    //
    // 卡面头部采用文言风格（"三转 · 冷却二"），因此中文数字参数 {RankCN}/{RemainingUsesCN}/{RecoveryTurnsCN}
    // 与阿拉伯数字参数并存：中文文案用前者，英文文案用后者。
    // {Rank1Exact}~{RankNExact} 供"只显示当前转数对应机制"的条件文本使用。
    //
    // 描述里引用的每个参数都必须在这里注入，否则卡面会字面显示未解析的占位符。
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);

        int remainingUses = GuCardRuntime.GetRemainingUses(this);

        description.Add("Rank", GuRank);
        description.Add("RankCN", ChineseNumber.ToChineseNumber(GuRank));
        description.Add("MaxUses", MaxUses);
        description.Add("RemainingUses", remainingUses);
        description.Add(
            "RemainingUsesCN",
            ChineseNumber.ToChineseNumber(remainingUses)
        );
        description.Add("RecoveryTurns", RecoveryDelayTurns);
        description.Add(
            "RecoveryTurnsCN",
            ChineseNumber.ToChineseNumber(RecoveryDelayTurns)
        );

        // 品阶区间按本牌自身的转数上限注入，转数更高的蛊牌同样可用。
        for (int rank = MinimumGuRank; rank <= MaxGuRank; rank++)
        {
            description.Add($"Rank{rank}Exact", GuRank == rank ? 1 : 0);
        }
    }

    // 为尚未初始化的蛊牌抽取初始品阶。均值会随楼层缓慢提高，但最高只提升到 3；标准差固定为 2。
    // 采样区间向整数边界各扩展 0.5，再四舍五入为整数，可让边缘品阶也获得自然的概率质量。
    //
    // maximumRank 用于仙蛊唯一性封顶：牌组中已有同名仙蛊时，调用方会把上限压到五转，
    // 使奖励永远不会直接产出第二张同名仙蛊。传入 null 时行为与旧逻辑逐位一致。
    internal bool TryAssignInitialRank(
        Rng rng,
        int totalFloor,
        int? maximumRank = null
    )
    {
        if (!NeedsInitialRankAssignment)
        {
            return false;
        }

        const double minimumMean = 1.0;
        const double maximumMean = 3.0;
        const double meanPerFloor = 0.05;
        const double standardDeviation = 2.0;

        int rankCap = Math.Clamp(
            maximumRank ?? MaxGuRank,
            MinimumGuRank,
            MaxGuRank
        );

        double mean = Math.Clamp(
            minimumMean + Math.Max(0, totalFloor - 1) * meanPerFloor,
            minimumMean,
            Math.Min(maximumMean, rankCap)
        );
        double sampleMin = MinimumGuRank - 0.5;
        double sampleMax = rankCap + 0.5;
        double sampleRange = sampleMax - sampleMin;
        double sampled = rng.NextGaussianDouble(
            (mean - sampleMin) / sampleRange,
            standardDeviation / sampleRange,
            sampleMin,
            sampleMax
        );

        SetGuRank((int)Math.Round(sampled));
        return true;
    }

    // 将品阶提升一级；达到最大品阶后不再提升，并通过返回值告诉调用方是否真的发生了变化。
    //
    // 升入仙蛊（六转及以上）时必须过唯一性仲裁：整局中已有同名仙蛊时本次升转会被拒绝，
    // 卡牌保持原转数不变，调用方据此提示玩家或直接不显示该候选。
    internal bool TryIncreaseGuRank()
    {
        if (GuRank >= MaxGuRank)
        {
            return false;
        }

        // 先算出目标转数：提交回调内不能再读 GuRank（那时它已经变了）。
        int targetRank = GuRank + 1;

        return GuXianGuRules.TryCommitGuRankIncrease(
            this,
            targetRank,
            () => SetGuRank(targetRank)
        );
    }

    // 升炼预览专用：只改本实例的显示转数，不做唯一性仲裁、不登记仙蛊。
    //
    // 原生升炼预览作用在 RunState.CloneCard 出来的克隆实例上，若走 TryIncreaseGuRank，
    // 预览就会触发真实仲裁（甚至降转别人的仙蛊），因此这里必须是独立的无仲裁入口。
    internal bool TryIncreaseGuRankForPreview()
    {
        if (GuRank >= MaxGuRank)
        {
            return false;
        }

        SetGuRank(GuRank + 1);
        return true;
    }

    // 仙蛊唯一性仲裁专用：把已经冲突的同名仙蛊恢复到五转。
    // 不触发升转奖励，只刷新依赖转数的派生状态。
    internal void ReconcileGuRankForUniqueness(int rank)
    {
        SetGuRank(Math.Clamp(rank, MinimumGuRank, MaxGuRank));
    }

    // 只读界面（配方大全等）需要展示"任意转数下的卡面与说明"。
    // 蛊牌不参与原生升级，原生没有转数预览入口，因此这里提供唯一的外部写入口。
    // 传入对象必须是可变副本（ToMutable），不得作用于卡池中的规范实例，否则会污染整局游戏的同一张卡。
    //
    // persist: false 表示"纯预览"——只改本实例的显示转数，不写 SavedAttachedState。
    // 预览副本是一次性的（每次刷新卡面都会重新克隆），写入附加状态只会给
    // 永远不会被存档或销毁的临时对象留下条目，因此这里必须可关闭。
    internal void InitializeGuRankForPreview(int rank, bool persist = false)
    {
        SetGuRank(Math.Clamp(rank, MinimumGuRank, MaxGuRank), persist);
    }

    // 在战斗构建或反序列化后重新同步品阶相关状态，并让子类重新计算依赖品阶的动态数值。
    internal void RefreshRankDerivedState()
    {
        _guRank = GuRank;
        _rankAssigned = RankAssignedState[this] || _rankAssigned;
        if (_rankAssigned && !RankAssignedState[this])
        {
            RankState[this] = _guRank;
            RankAssignedState[this] = true;
        }
        OnGuRankChanged();
    }

    // 子类可覆盖此钩子，在品阶改变或状态恢复后刷新伤害、格挡等派生数值。
    protected virtual void OnGuRankChanged()
    {
    }

    // 存档反序列化完成后立即恢复本地缓存，避免显示值与持久化品阶不一致。
    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        RefreshRankDerivedState();
    }

    // 统一的品阶写入口：写入合法值、按需标记已初始化，并触发子类的数值刷新钩子。
    private void SetGuRank(int rank, bool persist = true)
    {
        GuRank = rank;

        if (!persist)
        {
            return;
        }

        _rankAssigned = true;
        RankAssignedState[this] = true;
        OnGuRankChanged();
    }

    // =========================================================
    // 合练（HeLian）入口
    // =========================================================

    /// <summary>
    /// 使用命中配方的全部合练材料初始化结果牌。
    ///
    /// 该入口位于公共蛊牌父类，因此声明配方的常规蛊牌也可以作为合练结果，
    /// 而不要求继承专用的合练牌父类。结果转数由 <see cref="CalculateHeLianResultRank"/>
    /// 决定；全局规则是「材料最高转数 + 1」，九转终点这类配方才重写为固定转数。
    /// </summary>
    internal void InitializeFromHeLian(IReadOnlyList<CardModel> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);

        if (materials.Count < 2)
        {
            throw new ArgumentException(
                "合练至少需要两张材料牌。",
                nameof(materials)
            );
        }

        SetGuRank(CalculateHeLianResultRank(materials));
        OnHeLianCompleted(materials);
    }

    /// <summary>
    /// 计算合练结果转数。全局规则：结果转数 = 材料最高转数 + 1，
    /// 再夹进本蛊自身的转数窗口（低于一转按一转，超出 MaxGuRank 按上限封顶）。
    /// 九转终点这类固定转数配方（如光蛊）才重写本方法。
    /// </summary>
    protected virtual int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        int highest = materials
            .OfType<IGuCard>()
            .Select(gu => Math.Max(MinimumGuRank, gu.GuRank))
            .DefaultIfEmpty(MinimumGuRank)
            .Max();

        return Math.Clamp(highest + 1, MinimumGuRank, MaxGuRank);
    }

    /// <summary>
    /// 卡牌由合练生成并写入转数后的扩展钩子。
    /// </summary>
    protected virtual void OnHeLianCompleted(
        IReadOnlyList<CardModel> materials
    )
    {
    }
}
