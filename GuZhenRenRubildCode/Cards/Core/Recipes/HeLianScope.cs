using GuZhenRenRubild.Common.Scoping;

namespace GuZhenRenRubild.Cards.Core.Recipes;

/// <summary>
/// 标记当前异步调用链正在执行永久牌组合练。
///
/// 合练材料从牌组移除时，其他系统（伴生牌保护、删牌惩罚等）
/// 可以据此识别"这是一次合练消耗"，从而避免按普通删牌处理。
/// </summary>
internal static class HeLianScope
{
    private static readonly AmbientFlag Active = new();

    internal static bool IsActive => Active.IsActive;

    internal static IDisposable Enter() => Active.Enter();
}
