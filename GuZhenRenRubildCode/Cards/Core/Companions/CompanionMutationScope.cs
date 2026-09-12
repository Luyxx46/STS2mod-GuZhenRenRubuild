using System.Threading;

namespace GuZhenRenRubild.Cards.Companions;

/// <summary>
/// 只供关系系统执行级联创建/删除时绕过玩家侧保护。AsyncLocal 支持异步与嵌套调用。
/// </summary>
internal static class CompanionMutationScope
{
    private static readonly AsyncLocal<int> Depth = new();

    internal static bool IsInternalMutationAllowed => Depth.Value > 0;

    internal static IDisposable AllowInternalMutation()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Depth.Value = Math.Max(0, Depth.Value - 1);
            _disposed = true;
        }
    }
}
