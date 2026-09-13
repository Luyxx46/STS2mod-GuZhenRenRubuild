namespace GuZhenRenRubild.Cards.Core.Abstractions;

/// <summary>
/// 所有真正蛊牌共享的最小能力接口，只暴露激活、恢复、元气费用和品阶系统必须读取的数据。
/// 其他玩法系统应在此接口之上扩展，而不是把额外职责继续塞进蛊牌核心生命周期中。
/// </summary>
public interface IGuCard
{
    // 当前品阶以及允许达到的最高品阶。
    int GuRank { get; }
    int MaxGuRank { get; }

    // 单次激活周期允许使用的总次数。
    int MaxUses { get; }

    // 每次打出需要消耗的元气数量。
    int YuanQiCost { get; }

    // 使用次数耗尽后，从进入恢复区到再次可用之间需要等待的回合数。
    int RecoveryDelayTurns { get; }
}
