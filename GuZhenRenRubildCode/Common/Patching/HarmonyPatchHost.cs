using HarmonyLib;

namespace GuZhenRenRubild.Common.Patching;

/// <summary>
/// 单个 Harmony 补丁组的生命周期持有者：持有 harmonyId 与"是否已初始化"状态，
/// 把"幂等初始化 / 反初始化"这段所有补丁都要写一遍的样板集中到一处。
///
/// 语义与逐个补丁手写的 <c>_initialized</c> + <c>new Harmony(id)</c> 完全一致：
/// <list type="bullet">
/// <item><see cref="TryInitialize"/> 已初始化时直接返回 false，不重复挂补丁。</item>
/// <item>configure 抛异常时不置位初始化标记，也<b>不</b>自动 Unpatch —— 与现状一致：
/// <see cref="Entry"/> 只回滚"已经成功初始化"的组件，因此失败的那个补丁组会保留
/// 它已经挂上的部分；此时再次调用 TryInitialize 会重新执行 configure（Harmony 自身
/// 对重复挂载同一 postfix 是幂等的），和手写实现的行为相同。</item>
/// <item><see cref="Unpatch"/> 等价于 <c>new Harmony(id).UnpatchAll(id)</c>，
/// 随后执行可选清理回调，最后置回未初始化。</item>
/// </list>
///
/// 需要"找不到目标就只警告、不抛异常"或"configure 失败时自行回滚"的补丁
/// （例如 <c>GuHandLayoutPatch</c>）语义不同，应保持手写实现，不要套用本类。
/// </summary>
internal sealed class HarmonyPatchHost(string harmonyId)
{
    /// <summary>本补丁组在 Harmony 中的唯一标识，原样传给 <see cref="Harmony"/>。</summary>
    internal string HarmonyId { get; } = harmonyId;

    /// <summary>本补丁组是否已经成功完成过一次初始化。</summary>
    internal bool IsInitialized { get; private set; }

    /// <summary>
    /// 幂等初始化：已初始化时返回 false。否则先执行 configure，只有在 configure
    /// 正常返回后才置位初始化标记。
    /// </summary>
    internal bool TryInitialize(Action<Harmony> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (IsInitialized)
        {
            return false;
        }

        configure(new Harmony(HarmonyId));

        IsInitialized = true;
        return true;
    }

    /// <summary>
    /// 反初始化：按 harmonyId 解除本补丁组的全部补丁，执行可选清理回调，
    /// 然后允许再次初始化。
    /// </summary>
    internal void Unpatch(Action? onUnpatched = null)
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        onUnpatched?.Invoke();
        IsInitialized = false;
    }
}
