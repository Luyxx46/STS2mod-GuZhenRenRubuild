using Godot;

using MegaCrit.Sts2.Core.Helpers;

namespace GuZhenRenRubild.Ui;

/// <summary>
/// 在游戏根节点上维护配方大全界面。
///
/// 界面本身只读取配方注册表，不参与任何战斗状态、随机数或多人同步，
/// 因此整个系统只有"挂一个 CanvasLayer"这一件事；顶栏按钮由界面自己
/// 在每 0.25 秒的锚点扫描中对位，顶栏重建后会自动重新挂载。
///
/// 时序要求：模组初始化发生在 <c>OneTimeInitialization.ExecuteVeryEarly</c> 中
/// （<c>ModManager.Initialize</c> 内部），而 <c>LocManager.Initialize</c>、
/// <c>ModelDb.Init</c> 与图集资源加载器都在其后的 <c>ExecuteEssential</c> 里才就绪。
/// 因此这里必须用延迟挂载，让界面的 <c>_Ready</c> 至少推迟一帧执行，
/// 否则首个本地化取值就会撞上尚未初始化的 <c>LocManager</c>。
/// </summary>
internal static class RecipeCompendiumSystem
{
    /// <summary>配方大全场景路径；场景只声明骨架，样式与内容由脚本写入。</summary>
    private const string ScenePath =
        "res://GuZhenRenRubild/scenes/ui/RecipeCompendium.tscn";

    private static RecipeCompendiumOverlay? _overlay;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            // 配方大全是纯装饰性界面：任何失败都只记日志，绝不把异常抛回
            // 模组初始化（那里会按逆序回滚全部已启动组件，代价远大于少一个界面）。
            Entry.Logger.Warn("配方大全未能挂载：当前主循环不是 SceneTree。");
            return;
        }

        if (!ResourceLoader.Exists(ScenePath))
        {
            Entry.Logger.Warn("配方大全未能挂载：场景缺失 " + ScenePath);
            return;
        }

        try
        {
            PackedScene scene = GD.Load<PackedScene>(ScenePath);
            RecipeCompendiumOverlay overlay = scene
                .Instantiate<RecipeCompendiumOverlay>();

            _overlay = overlay;

            // 延迟到本帧空闲时再入树：必须在 ExecuteEssential 完成之后再执行 _Ready。
            tree.Root.CallDeferred(Node.MethodName.AddChild, overlay);
            _initialized = true;
            Entry.Logger.Info("配方大全界面已挂载（延迟入树）。");
        }
        catch (Exception exception)
        {
            _overlay = null;
            Entry.Logger.Warn("配方大全未能挂载：" + exception);
        }
    }

    internal static void Uninitialize()
    {
        RecipeCompendiumOverlay? overlay = _overlay;
        _overlay = null;
        _initialized = false;

        if (GodotObject.IsInstanceValid(overlay))
        {
            overlay!.QueueFreeSafely();
        }
    }
}
