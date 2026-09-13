using Godot;

using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.TopBar;

namespace GuZhenRenRubild.Ui;

/// <summary>
/// 顶栏「配方大全」按钮。
///
/// 用原生小地图按钮的控件树复制而成，因此天然保留游戏的输入、焦点、
/// 音效、着色器与悬停动画表现；只有图标被替换成配方大全图。
/// 由 <see cref="RecipeCompendiumOverlay"/> 在顶栏就绪后挂到小地图按钮左侧。
/// </summary>
public sealed partial class RecipeCompendiumTopBarButton : NTopBarButton
{
    // 原生按钮着色器的亮度参数名，按下时压暗。
    private static readonly StringName ShaderValue = new("v");

    // 悬停提示表与键名；键名与 localization/**/static_hover_tips.json 对应。
    private const string HoverTipTable = "static_hover_tips";
    private const string HoverTipKeyPrefix =
        "GU_ZHEN_REN_RUBILD_RECIPE_COMPENDIUM";

    private const float PressedShaderValue = 0.9f;

    private RecipeCompendiumOverlay _overlay = null!;

    /// <summary>
    /// 依据原生小地图按钮创建一个同形的配方大全按钮。
    /// 复制控件树失败时抛异常，由调用方记录并跳过本次挂载。
    /// </summary>
    public static RecipeCompendiumTopBarButton Create(
        NTopBarMapButton mapButton,
        RecipeCompendiumOverlay overlay
    )
    {
        ArgumentNullException.ThrowIfNull(mapButton);
        ArgumentNullException.ThrowIfNull(overlay);

        RecipeCompendiumTopBarButton button = new()
        {
            Name = "RecipeCompendiumButton",
            FocusMode = mapButton.FocusMode,
            MouseFilter = mapButton.MouseFilter,
            ProcessMode = mapButton.ProcessMode,
        };
        button._overlay = overlay;

        Control visual =
            mapButton.GetNode<Control>("Control").Duplicate() as Control ??
            throw new InvalidOperationException(
                "无法复制原生地图按钮的控件树。"
            );
        visual.Name = "Control";
        visual.MouseFilter = Control.MouseFilterEnum.Ignore;

        Control icon = visual.GetNode<Control>("Icon");
        ApplyCompendiumTexture(icon);
        button.AddChild(visual);

        // 原生地图按钮的根节点是 MarginContainer，复制出的 Control 原本依赖
        // 该容器获得尺寸。自定义按钮由代码直接创建，其根节点只是普通 Control，
        // 因此必须显式铺满，否则 Icon 会保持零尺寸。
        visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        visual.OffsetTop = 8f;
        visual.OffsetBottom = -8f;

        // 配方大全没有对应的地图快捷键，避免显示复制来的快捷键标记。
        visual.GetNodeOrNull<Control>("HotkeyIcon")?.Hide();

        return button;
    }

    /// <summary>把按钮的"界面已打开"着色状态与配方面板同步。</summary>
    public void RefreshOpenState() => UpdateScreenOpen();

    public override void _Ready()
    {
        // 必须让基类先跑：它会把 Control/Icon 与其材质缓存到 _icon / _hsv。
        // 只有在这之后，才能安全地把材质换成克隆件并重新缓存 _hsv，
        // 否则动画与按下着色会写到原生小地图按钮的材质上。
        base._Ready();

        if (_icon?.Material is ShaderMaterial shared &&
            shared.Duplicate() is ShaderMaterial ownMaterial)
        {
            _icon.Material = ownMaterial;
            _hsv = ownMaterial;
        }
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        _overlay.ToggleDialog();
        UpdateScreenOpen();
        _hsv?.SetShaderParameter(ShaderValue, PressedShaderValue);
    }

    protected override bool IsOpen() => _overlay.IsDialogOpen;

    protected override void OnFocus()
    {
        base.OnFocus();

        HoverTip hoverTip = new(
            new LocString(
                HoverTipTable,
                HoverTipKeyPrefix + ".title"
            ),
            new LocString(
                HoverTipTable,
                HoverTipKeyPrefix + ".description"
            )
        );
        NHoverTipSet? tipSet = NHoverTipSet.CreateAndShow(this, hoverTip);

        // 与原生顶栏按钮一致：提示靠按钮右缘对齐，位于按钮下方。
        tipSet?.SetGlobalPosition(
            GlobalPosition +
            new Vector2(Size.X - tipSet.Size.X, Size.Y + 20f)
        );
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        NHoverTipSet.Remove(this);
    }

    private static void ApplyCompendiumTexture(Control icon)
    {
        Texture2D? texture = RecipeCompendiumOverlay.LoadCompendiumTexture();

        if (texture == null)
        {
            return;
        }

        if (!TryApplyTexture(icon, texture))
        {
            Entry.Logger.Warn(
                "未在原生地图按钮 Icon 节点中找到可替换纹理的子节点；" +
                "已保留地图按钮原图作为兜底。"
            );
        }
    }

    private static bool TryApplyTexture(Node node, Texture2D texture)
    {
        switch (node)
        {
            case TextureRect textureRect:
                textureRect.Texture = texture;
                return true;
            case TextureButton textureButton:
                textureButton.TextureNormal = texture;
                textureButton.TexturePressed = texture;
                textureButton.TextureHover = texture;
                textureButton.TextureFocused = texture;
                return true;
            case NinePatchRect ninePatchRect:
                ninePatchRect.Texture = texture;
                return true;
            case Sprite2D sprite:
                sprite.Texture = texture;
                return true;
        }

        foreach (Node child in node.GetChildren())
        {
            if (TryApplyTexture(child, texture))
            {
                return true;
            }
        }

        return false;
    }
}
