using System.Threading;

namespace GuZhenRenRubild.Common.Scoping;

/// <summary>
/// 可重入的"环境标记"开关：标记当前异步调用链是否正处于某个受控操作内部。
///
/// 状态保存在 <see cref="AsyncLocal{T}"/> 中，因此会随 async/await 调用链一起流动，
/// 不会泄漏到其它玩家的并发流程；嵌套进入按深度计数，只有最外层作用域结束后
/// <see cref="IsActive"/> 才会回到 false。Harmony 补丁正是靠它区分
/// "玩家侧的直接操作"与"模组内部的级联操作"。
///
/// 用法：
/// <code>
/// using (Scope.Enter())
/// {
///     // 此处 Scope.IsActive 为 true，且补丁会放行内部改动。
/// }
/// </code>
/// </summary>
public sealed class AmbientFlag
{
    private readonly AsyncLocal<int> _depth = new();

    /// <summary>当前调用链是否处于标记作用域内。</summary>
    public bool IsActive => _depth.Value > 0;

    /// <summary>当前嵌套深度，未进入时为 0。</summary>
    public int Depth => _depth.Value;

    /// <summary>
    /// 进入标记作用域。返回值释放时退出；重复释放只生效一次，
    /// 因此可以安全地同时交给 postfix 与 finalizer。
    /// </summary>
    public IDisposable Enter()
    {
        _depth.Value++;
        return new Scope(this);
    }

    /// <summary>
    /// 强制清空当前异步上下文中的深度，供反初始化/回滚路径使用。
    /// 与进入作用域一样，只影响当前执行上下文。
    /// </summary>
    public void Reset() => _depth.Value = 0;

    private sealed class Scope(AmbientFlag owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            // 极端情况下（异步上下文被复制）深度可能已被外部重置，这里只做保护性递减。
            if (owner._depth.Value > 0)
            {
                owner._depth.Value--;
            }
        }
    }
}
