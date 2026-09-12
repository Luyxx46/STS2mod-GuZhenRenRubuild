using GuZhenRenRubild.Common.Scoping;

namespace GuZhenRenRubild.Cards.Companions;

/// <summary>
/// 只供关系系统执行级联创建/删除时绕过玩家侧保护。AsyncLocal 支持异步与嵌套调用。
/// </summary>
internal static class CompanionMutationScope
{
    private static readonly AmbientFlag InternalMutation = new();

    internal static bool IsInternalMutationAllowed => InternalMutation.IsActive;

    internal static IDisposable AllowInternalMutation() =>
        InternalMutation.Enter();
}
