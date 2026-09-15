namespace GuZhenRenRubild.Cards.Core.Abstractions;

/// <summary>
/// 声明本牌固定悬停于蛊手牌最左侧区域。
///
/// 与蛊牌不同，这类系统牌不占蛊牌补位槽（蛊手牌容量只统计
/// <see cref="IGuCard"/>，见 <c>GuCardPileSystem.CountActiveGu</c>），
/// 也不参与任何牌堆循环；它们只会由系统在战斗开始时发放，
/// 并按 <see cref="GuHandOrderRank"/> 升序稳定排列在蛊手牌左端。
/// </summary>
public interface IGuHandPinnedCard
{
    /// <summary>
    /// 固定位次序，越小越靠左；同秩牌保持牌堆现有相对顺序。
    /// 非悬停牌视为正无穷，永远排在所有悬停牌右侧。
    /// </summary>
    int GuHandOrderRank { get; }
}
