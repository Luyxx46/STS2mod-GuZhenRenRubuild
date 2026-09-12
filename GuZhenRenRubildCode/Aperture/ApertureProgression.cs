namespace GuZhenRenRubild.Aperture;

/// <summary>
/// 空窍/仙窍升级状态机的纯规则层。
///
/// 一至五转沿用普通/精英/首领战的修为收益；
/// 五转修为满后直接进入六转（仙窍）；
/// 六至八转每次战斗胜利获得 1 点仙窍进度；
/// 九转为当前实现上限。
///
/// 本类不接触存档、界面与游戏 API，便于单独推演数值。
/// </summary>
public static class ApertureProgression
{
    public const int MinimumRank = 1;

    /// <summary>
    /// 六转起进入仙窍阶段。
    /// </summary>
    public const int ImmortalRank = 6;

    /// <summary>
    /// 九转是当前已实现终点。
    /// </summary>
    public const int MaximumImplementedRank = 9;

    /// <summary>
    /// 元气容量的最大值，即九转（<see cref="MaximumImplementedRank"/>）对应的容量。
    /// 供副资源定义用作 hardMaxAmount：即使遗物改写上限的钩子缺席，也不会超出已实现曲线。
    /// 必须与 <see cref="YuanQiCapacityByRank"/> 的最大值（即 [9] = 9）保持一致。
    /// </summary>
    public const int MaximumYuanQiCapacity = 9;

    /// <summary>
    /// 各转数突破所需的修为。九转为终点，无需再突破。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> RequiredXpByRank =
        new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 2,
            [3] = 3,
            [4] = 4,
            [5] = 5,
            [6] = 2,
            [7] = 2,
            [8] = 3,
            [9] = 0,
        };

    /// <summary>
    /// 空窍转数对应的元气容量上限，曲线 3、4、4、5、5、7、7、8、9。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int>
        YuanQiCapacityByRank = new Dictionary<int, int>
        {
            [1] = 3,
            [2] = 4,
            [3] = 4,
            [4] = 5,
            [5] = 5,
            [6] = 7,
            [7] = 7,
            [8] = 8,
            [9] = 9,
        };

    /// <summary>
    /// 每回合元气回复量，由当前元气上限决定：
    /// 上限 3 回复 1、上限 4~5 回复 2、上限 6~7 回复 3、上限 8~9 回复 4。
    /// 对应一至九转曲线为 1、2、2、2、2、3、3、4、4。
    /// </summary>
    public static int GetYuanQiRecovery(int rank)
    {
        int capacity = GetYuanQiCapacity(rank);

        return capacity switch
        {
            <= 3 => 1,
            <= 5 => 2,
            <= 7 => 3,
            _ => 4,
        };
    }

    /// <summary>
    /// 每场战斗开始时的元气量：补满至当前转数上限。
    /// </summary>
    public static int GetYuanQiStartAmount(int rank) =>
        GetYuanQiCapacity(rank);

    public static int GetRequiredXp(int rank) =>
        RequiredXpByRank.TryGetValue(rank, out int value) ? value : 0;

    /// <summary>
    /// 突破到该转数时获得的最大生命奖励；凡人阶段（一至五转）没有奖励。
    /// </summary>
    public static int GetMaxHpAward(int rank)
    {
        return rank switch
        {
            6 => 2,
            7 => 2,
            8 => 3,
            9 => 3,
            _ => 0,
        };
    }

    public static int GetYuanQiCapacity(int rank)
    {
        int normalizedRank = Math.Clamp(
            rank,
            MinimumRank,
            MaximumImplementedRank
        );

        return YuanQiCapacityByRank[normalizedRank];
    }

    /// <summary>
    /// 结算一场胜利提供的修为。
    /// 一场战斗最多提升一个转数，溢出修为保留到下一转。
    /// </summary>
    public static ApertureTransition GainVictoryXp(
        ApertureRunData data,
        int mortalXp
    )
    {
        ArgumentNullException.ThrowIfNull(data);
        data.Normalize();

        if (data.IsCultivationComplete)
        {
            return ApertureTransition.None(data.Rank);
        }

        // 凡窍阶段按战斗难度给修为；仙窍阶段固定每场 1 点。
        int amount = data.Rank < ImmortalRank
            ? Math.Max(0, mortalXp)
            : 1;

        if (amount <= 0)
        {
            return ApertureTransition.None(data.Rank);
        }

        int previousRank = data.Rank;
        int requiredXp = GetRequiredXp(previousRank);
        data.Xp += amount;

        if (requiredXp <= 0 || data.Xp < requiredXp)
        {
            return ApertureTransition.Progress(previousRank);
        }

        int overflow = data.Xp - requiredXp;
        data.Rank++;

        // 五转进入仙窍，以及仙窍阶段之间的突破，都从新阶段的 0 点进度开始。
        data.Xp = previousRank >= ImmortalRank - 1 ? 0 : overflow;

        if (data.Rank >= MaximumImplementedRank)
        {
            data.Rank = MaximumImplementedRank;
            data.Xp = 0;
            data.IsCultivationComplete = true;
        }

        return ApertureTransition.RankAdvanced(
            previousRank,
            data.Rank,
            data.IsCultivationComplete
        );
    }
}

/// <summary>
/// 一次胜利结算后的转数变化结果。
/// </summary>
public readonly record struct ApertureTransition(
    int PreviousRank,
    int CurrentRank,
    bool RankChanged,
    bool CultivationComplete
)
{
    public static ApertureTransition None(int rank) =>
        new(rank, rank, false, false);

    public static ApertureTransition Progress(int rank) =>
        new(rank, rank, false, false);

    public static ApertureTransition RankAdvanced(
        int previousRank,
        int currentRank,
        bool cultivationComplete
    ) =>
        new(previousRank, currentRank, true, cultivationComplete);
}
