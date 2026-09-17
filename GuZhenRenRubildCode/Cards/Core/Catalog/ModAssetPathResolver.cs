using Godot;

namespace GuZhenRenRubild.Cards.Core.Catalog;

/// <summary>
/// 可选美术资源的路径解析：<b>专属资源存在就用专属资源，缺失时回退到既有资源</b>。
///
/// <para>
/// 本模组的卡图与图标大量复用两张模板图（攻击向 <c>GuZhenRenRubildStrike.png</c>、
/// 防御向 <c>GuZhenRenRubildDefend.png</c>）。约定一张卡 <c>Foo</c> 的专属卡图是
/// <c>res://GuZhenRenRubild/images/cards/Foo.png</c>；只要把同名 PNG 放进目录，
/// 导出时会被 <c>export_preset</c> 的 <c>all_resources</c> 自动打包，
/// 卡面立刻换图，<b>不需要改任何代码</b>。
/// </para>
///
/// <para>
/// 结果按路径缓存：<see cref="ResourceLoader.Exists"/> 每次都会触碰资源系统，
/// 而卡图与图标路径在渲染、悬浮提示与图鉴里会被反复读取。
/// <b>只有探测成功才缓存</b>——资源系统尚未就绪时探测会抛异常，那次结果不缓存、
/// 下次访问重试，避免把「暂时探测不到」永久固化成「这张图不存在」。
/// 探测始终失败时行为与"写死回退路径"完全一致，绝不把不存在的路径交给引擎。
/// </para>
/// </summary>
internal static class ModAssetPathResolver
{
    /// <summary>模板卡图：攻击向。</summary>
    internal const string StrikeTemplate = "GuZhenRenRubildStrike.png";

    /// <summary>模板卡图：防御向。</summary>
    internal const string DefendTemplate = "GuZhenRenRubildDefend.png";

    private static readonly Dictionary<string, string?> Cache =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);

    private static readonly object SyncRoot = new();

    /// <summary>
    /// 解析一张卡牌的卡图：优先 <c>images/cards/&lt;类名&gt;.png</c>，
    /// 缺失时用 <paramref name="templateFileName"/> 对应的模板图。
    /// </summary>
    internal static string ResolveCardPortrait(
        string cardTypeName,
        string templateFileName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardTypeName);

        return ResolveOptional(
                $"{Entry.ResPath}/images/cards/{cardTypeName}.png",
                $"{Entry.ResPath}/images/cards/{templateFileName}"
            )
            ?? $"{Entry.ResPath}/images/cards/{templateFileName}";
    }

    /// <summary>
    /// 解析任意可选资源：<paramref name="dedicatedPath"/> 存在时返回它，
    /// 否则返回 <paramref name="fallbackPath"/>（可为 null，表示"不覆盖"）。
    /// </summary>
    internal static string? ResolveOptional(
        string dedicatedPath,
        string? fallbackPath
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dedicatedPath);

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(dedicatedPath, out string? cached))
            {
                return cached;
            }
        }

        string? resolved = fallbackPath;
        bool probeSucceeded;

        try
        {
            probeSucceeded = true;
            if (ResourceLoader.Exists(dedicatedPath))
            {
                resolved = dedicatedPath;
            }
        }
        catch (Exception exception)
        {
            // 资源系统尚未就绪时会走到这里。此时**不缓存**结果：
            // 缓存了就等于把"暂时探测不到"永久固化成"这张图不存在"，
            // 之后补上的专属图将永远不生效。
            probeSucceeded = false;
            WarnOnce(
                dedicatedPath,
                $"美术资源解析：检查 {dedicatedPath} 时失败，本次回退 " +
                $"{fallbackPath ?? "<无>"}；将稍后重试：{exception.Message}"
            );
        }

        if (probeSucceeded)
        {
            lock (SyncRoot)
            {
                Cache[dedicatedPath] = resolved;
            }
        }

        return resolved;
    }

    private static void WarnOnce(string key, string message)
    {
        lock (SyncRoot)
        {
            if (!Warned.Add(key))
            {
                return;
            }
        }

        Entry.Logger.Warn(message);
    }
}
