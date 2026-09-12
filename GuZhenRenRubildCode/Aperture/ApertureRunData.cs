namespace GuZhenRenRubild.Aperture;

/// <summary>
/// 空窍转数与修为的运行期数据。
/// 由 RitsuLib 的 Run Saved Data 按玩家保存，并随运行快照同步，
/// 因此断线重连与多人恢复都不需要额外处理。
/// </summary>
public sealed class ApertureRunData
{
    public int Xp { get; set; }

    public int Rank { get; set; } = ApertureProgression.MinimumRank;

    /// <summary>
    /// 九转是当前已实现终点，到达后不再累积修为。
    /// </summary>
    public bool IsCultivationComplete { get; set; }

    /// <summary>
    /// 已结算胜利修为的最后一个运行层数，防止胜利回调重放时重复加修为。
    /// </summary>
    public int VictoryXpAppliedFloor { get; set; } = -1;

    /// <summary>
    /// 已成功结算最大生命奖励的最高转数。
    /// </summary>
    public int MaxHpAppliedThroughRank { get; set; }

    /// <summary>
    /// 正在结算最大生命奖励的转数，以及命令执行前的最大生命。
    /// 两者共同用于识别"命令已成功、进度标记尚未提交"的断线窗口。
    /// </summary>
    public int MaxHpAwardInProgressRank { get; set; }

    public int MaxHpBeforePendingAward { get; set; }

    /// <summary>
    /// 用于兼容旧存档：首次读取旧数据时，把副作用进度视为已完成到当前转数，
    /// 防止更新模组后重复获得历史最大生命奖励。
    /// </summary>
    public bool SideEffectProgressInitialized { get; set; }

    public bool NeedsNormalization()
    {
        int normalizedRank = Math.Clamp(
            Rank,
            ApertureProgression.MinimumRank,
            ApertureProgression.MaximumImplementedRank
        );

        if (Rank != normalizedRank ||
            Xp < 0 ||
            VictoryXpAppliedFloor < -1)
        {
            return true;
        }

        bool shouldBeComplete =
            normalizedRank >= ApertureProgression.MaximumImplementedRank;

        if (IsCultivationComplete != shouldBeComplete ||
            (shouldBeComplete && Xp != 0) ||
            !SideEffectProgressInitialized)
        {
            return true;
        }

        // 凡人阶段没有最大生命奖励，进度必须始终等于当前转数。
        int minimumAppliedRank =
            normalizedRank < ApertureProgression.ImmortalRank
                ? normalizedRank
                : ApertureProgression.MinimumRank;

        if (MaxHpAppliedThroughRank < minimumAppliedRank ||
            MaxHpAppliedThroughRank > normalizedRank)
        {
            return true;
        }

        bool hasMaxHpAwardInProgress =
            MaxHpAwardInProgressRank != 0 ||
            MaxHpBeforePendingAward != 0;

        return hasMaxHpAwardInProgress &&
               (MaxHpAwardInProgressRank < ApertureProgression.ImmortalRank ||
                MaxHpAwardInProgressRank <= MaxHpAppliedThroughRank ||
                MaxHpAwardInProgressRank > normalizedRank ||
                MaxHpBeforePendingAward <= 0);
    }

    public void Normalize()
    {
        Rank = Math.Clamp(
            Rank,
            ApertureProgression.MinimumRank,
            ApertureProgression.MaximumImplementedRank
        );
        Xp = Math.Max(0, Xp);
        VictoryXpAppliedFloor = Math.Max(-1, VictoryXpAppliedFloor);

        if (Rank >= ApertureProgression.MaximumImplementedRank)
        {
            Rank = ApertureProgression.MaximumImplementedRank;
            Xp = 0;
            IsCultivationComplete = true;
        }
        else
        {
            IsCultivationComplete = false;
        }

        if (!SideEffectProgressInitialized)
        {
            // 旧存档没有进度字段。假定历史转数的副作用已经结算，
            // 避免升级模组后重复加最大生命。
            MaxHpAppliedThroughRank = Rank;
            MaxHpAwardInProgressRank = 0;
            MaxHpBeforePendingAward = 0;
            SideEffectProgressInitialized = true;
        }

        if (Rank < ApertureProgression.ImmortalRank)
        {
            MaxHpAppliedThroughRank = Rank;
        }
        else
        {
            MaxHpAppliedThroughRank = Math.Clamp(
                MaxHpAppliedThroughRank,
                ApertureProgression.MinimumRank,
                Rank
            );
        }

        bool validMaxHpAwardInProgress =
            MaxHpAwardInProgressRank >= ApertureProgression.ImmortalRank &&
            MaxHpAwardInProgressRank > MaxHpAppliedThroughRank &&
            MaxHpAwardInProgressRank <= Rank &&
            MaxHpBeforePendingAward > 0;

        if (!validMaxHpAwardInProgress)
        {
            MaxHpAwardInProgressRank = 0;
            MaxHpBeforePendingAward = 0;
        }
    }
}
