using GuZhenRenRubild.Common.Scoping;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 标记当前异步调用链正在执行杀招封装。
///
/// 材料被移出蛊虫循环时，其他系统可以据此区分"杀招封装消耗"
/// 与普通永久删牌行为，避免误触发删牌相关的惩罚或保护逻辑。
/// </summary>
internal static class ShaZhaoSynthesisScope
{
    private static readonly AmbientFlag Active = new();

    internal static bool IsActive => Active.IsActive;

    internal static IDisposable Enter() => Active.Enter();
}
