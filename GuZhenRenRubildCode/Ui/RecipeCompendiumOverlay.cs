using Godot;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Catalog;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Common.Text;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRenRubild.Ui;

/// <summary>
/// 顶栏「蛊方大全」按钮与只读浏览器（仅在冒险进行中显示）。
///
/// 四个页签：蛊虫大全（按名称查询蛊虫）、卡牌大全（收录本模组全部卡牌，
/// 含伴生牌、杀招推演系统牌与杀招牌）、合练配方、杀招配方。
/// 每张牌都有自己的查看页面，页面只插入卡牌贴图，**不渲染原生卡面**，
/// 并展示卡面之外的"详细说明"（<c>&lt;CARD_KEY&gt;.detailDescription</c>）；
/// 蛊虫页还会额外展示获取方式、合练路径、杀招路径与伴生牌。
///
/// 界面骨架与全部静态样式（面板/行/页签/按钮 StyleBox、字体大小、颜色、
/// 文案默认值）都写在场景
/// <c>res://GuZhenRenRubild/scenes/ui/RecipeCompendium.tscn</c> 里，
/// 这样在 Godot 编辑器里打开场景就能直接看到成品外观。
/// 脚本运行时只负责：本地化文案、程序化资源（宣纸底纹、图鉴图标）、
/// 数据驱动内容（列表行、转数按钮、贴图）与页面栈。
///
/// 本类同时是 <c>[Tool]</c> 脚本：编辑器里显示底图并塞一份示例，
/// 便于直接预览版式；真实数据与贴图只在游戏内出现。
/// </summary>
[Tool]
public partial class RecipeCompendiumOverlay : CanvasLayer
{
    // 配方大全专用图标，从旧模组 GuZhenRenPersonal 移植（旧文件名是全小写）。
    private const string ModCompendiumIconPath =
        "res://GuZhenRenRubild/images/ui/RecipeCompendiumIcon.png";

    // 原版图鉴图标；取不到时回退小地图图标。
    private const string BuiltInCompendiumIconPath =
        "res://images/atlases/ui_atlas.sprites/compendium.tres";

    private const string FallbackMapIconPath =
        "res://images/atlases/ui_atlas.sprites/top_bar/top_bar_map.tres";

    /// <summary>
    /// 顶栏按钮与面板标题所用的图鉴图标候选路径，按优先级排列。
    ///
    /// 首选本模组专用图（旧模组移植）；它缺失时退回原版图鉴图，最后退回
    /// 小地图图标。全部失败时顶栏按钮保留复制来的原图，面板标题图标为空。
    /// </summary>
    private static readonly string[] IconPaths =
    [
        ModCompendiumIconPath,
        BuiltInCompendiumIconPath,
        FallbackMapIconPath,
    ];

    /// <summary>
    /// 界面文案所在的本地化表。
    ///
    /// 必须复用**原版已有的表名**：游戏的 <c>LocManager</c> 只会枚举原版
    /// <c>res://localization/&lt;lang&gt;</c> 下的文件名，再把各模组同名文件合并进
    /// 这些已存在的表。模组自创新表名永远不会被登记，取值会直接抛 <c>LocException</c>。
    /// <c>card_library</c> 就是原版的卡牌图鉴表，蛊方大全整块文案都放在这里。
    /// </summary>
    private const string LocTableName = "card_library";

    // 表内键名统一前缀，避免与游戏其他模组的键冲突。
    private const string LocKeyPrefix = "GU_ZHEN_REN_RUBILD_COMPENDIUM";

    // 顶栏按钮悬停提示仍在 static_hover_tips 表里（那是真正的悬停提示表）。
    private const string HoverTipKeyPrefix =
        "GU_ZHEN_REN_RUBILD_RECIPE_COMPENDIUM";

    // 自定义顶栏按钮与原生小地图按钮之间的间距。
    private const float TopBarButtonGap = 14f;

    // 主题色：与 GuYueFangYuan.ThemeColor 同一套水墨-青玉配色。
    // 面板/页签/返回按钮的静态样式与颜色已固化在 RecipeCompendium.tscn 里，
    // 这里只保留运行时动态创建的控件（列表行、链接按钮、转数按钮）要用的颜色。
    private static readonly Color Gold = new("d0a45e");
    private static readonly Color Cyan = new("69a6a8");
    private static readonly Color Ink = new("e7dfcb");
    private static readonly Color Muted = new("9aa3a1");
    private static readonly Color RowColor = new("202b30");
    private static readonly Color Cinnabar = new("c36d5a");

    // 顶栏按钮每 0.25 秒重新对位一次，顶栏重建后自动重新挂载。
    private const double AnchorScanIntervalSeconds = 0.25d;

    /// <summary>编辑器预览模式：场景在 Godot 里直接打开时生效。</summary>
    private bool _preview;

    // ---- 场景节点 ----
    private ColorRect _backdrop = null!;
    private PanelContainer _dialog = null!;
    private TextureRect _paperTexture = null!;
    private TextureRect _headerIcon = null!;
    private Button _guTab = null!;
    private Button _cardTab = null!;
    private Button _heLianTab = null!;
    private Button _shaZhaoTab = null!;
    private LineEdit _search = null!;
    private Label _summary = null!;
    private VBoxContainer _recipeRows = null!;
    private VBoxContainer _listView = null!;

    private VBoxContainer _cardDetailView = null!;
    private TextureRect _cardPortrait = null!;
    private Label _cardDetailTitle = null!;
    private Label _cardDetailMeta = null!;
    private RichTextLabel _cardDetailDescription = null!;
    private Label _cardDetailDetailSectionLabel = null!;
    private RichTextLabel _cardDetailDetailText = null!;
    private HFlowContainer _cardRankButtons = null!;
    private VBoxContainer _acquisitionContent = null!;
    private VBoxContainer _heLianPathContent = null!;
    private VBoxContainer _shaZhaoPathContent = null!;
    private HFlowContainer _companionContent = null!;

    private VBoxContainer _shaZhaoDetailView = null!;
    private TextureRect _shaZhaoPortrait = null!;
    private Label _shaZhaoDetailTitle = null!;
    private Label _shaZhaoDetailMeta = null!;
    private RichTextLabel _shaZhaoDetailDescription = null!;
    private Label _shaZhaoDetailDetailSectionLabel = null!;
    private RichTextLabel _shaZhaoDetailDetailText = null!;
    private VBoxContainer _shaZhaoMaterialsContent = null!;

    private Godot.Timer _anchorScanTimer = null!;

    // ---- 状态 ----
    private IReadOnlyList<CardModel> _guCards = [];
    private IReadOnlyList<CardModel> _allCards = [];
    private IReadOnlyList<RecipeViewModel> _recipes = [];
    private IReadOnlyDictionary<Type, CardModel> _cardModels =
        new Dictionary<Type, CardModel>();
    private RecipeCategory _selectedCategory = RecipeCategory.Gu;
    private Type? _selectedCardType;
    private int _selectedRank = AbstractGuCard.MinimumGuRank;

    /// <summary>
    /// 页面栈。列表页与两个详情页都可能互相跳转（蛊 → 配方 → 另一只蛊），
    /// 因此返回按钮与 Esc 都按栈逐层回退，不会一步跳回列表。
    /// </summary>
    private readonly Stack<Func<Control>> _pageStack = new();

    private RecipeCompendiumTopBarButton? _topBarButton;
    private NTopBarMapButton? _mapButton;
    private NodePath _originalMapFocusNeighborLeft = new();

    /// <summary>蛊方大全是否正在显示。</summary>
    public bool IsDialogOpen => _backdrop.Visible;

    // =================================================================
    //  生命周期
    // =================================================================

    public override void _Ready()
    {
        Layer = 92;
        ProcessMode = ProcessModeEnum.Always;

        if (!BindSceneNodes())
        {
            SetProcessInput(false);
            return;
        }

        _preview = Engine.IsEditorHint();

        // 纯装饰性界面：主题或文案的任何异常都只记日志，不抛出到 Godot 的
        // _Ready 调用栈，避免一个界面问题牵连整个模组。
        try
        {
            ApplyTheme();
        }
        catch (Exception exception)
        {
            Warn("蛊方大全主题初始化失败：" + exception);
        }

        if (_preview)
        {
            SetProcessInput(false);
            ShowPreviewContent();
            return;
        }

        ShowListPage();
        RefreshTopBarButton();

        // 旧实现为了每 0.25 秒找一次顶栏锚点而常驻 _Process；
        // 改用 Timer 后，关闭/闲置界面时不再每帧进入 C#。
        _anchorScanTimer = new Godot.Timer
        {
            Name = "RecipeCompendiumAnchorScanTimer",
            WaitTime = AnchorScanIntervalSeconds,
            OneShot = false,
            Autostart = true,
            ProcessMode = ProcessModeEnum.Always,
        };
        _anchorScanTimer.Timeout += OnAnchorScanTimerTimeout;
        AddChild(_anchorScanTimer);

        SetProcessInput(true);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_backdrop.Visible ||
            @event is not InputEventKey
            {
                Pressed: true,
                Echo: false,
                Keycode: Key.Escape,
            })
        {
            return;
        }

        // Esc 先逐层回退页面栈，栈空才关闭整个界面。
        if (_pageStack.Count > 0)
        {
            PopPage();
        }
        else
        {
            CloseDialog();
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        if (_anchorScanTimer != null &&
            GodotObject.IsInstanceValid(_anchorScanTimer))
        {
            _anchorScanTimer.Timeout -= OnAnchorScanTimerTimeout;
            _anchorScanTimer.Stop();
        }

        RemoveTopBarButton();
    }

    // =================================================================
    //  对外入口
    // =================================================================

    /// <summary>切换蛊方大全的显示状态。</summary>
    public void ToggleDialog()
    {
        if (_backdrop.Visible)
        {
            CloseDialog();
        }
        else
        {
            OpenDialog();
        }
    }

    /// <summary>
    /// 加载顶栏按钮与面板标题所用的图标。三级回退：
    /// 模组专用图 → 原版图鉴图 → 小地图图；全部不可用时返回 null，
    /// 顶栏按钮保留复制来的原图。
    ///
    /// 模组资源用 <see cref="ResourceLoader.Load{T}"/> 直接加载，**不走**
    /// <c>PreloadManager.Cache</c>：后者是游戏为原版资源设计的预加载缓存，
    /// 对它没预加载过的路径会打一条 "Asset not cached" 警告并登记为
    /// "错过缓存"，而模组自己的 PCK 资源不在游戏的预加载计划里，永远命中不了。
    /// </summary>
    public static Texture2D? LoadCompendiumTexture()
    {
        foreach (string path in IconPaths)
        {
            try
            {
                if (!ResourceLoader.Exists(path))
                {
                    continue;
                }

                Texture2D? texture = ResourceLoader.Load<Texture2D>(
                    path,
                    cacheMode: ResourceLoader.CacheMode.Reuse
                );

                if (texture != null && GodotObject.IsInstanceValid(texture))
                {
                    return texture;
                }
            }
            catch (Exception exception)
            {
                Warn($"蛊方大全图标加载失败（{path}）：{exception.Message}");
            }
        }

        Warn("蛊方大全图标及全部回退图都无法加载。");
        return null;
    }

    // =================================================================
    //  节点绑定与主题
    // =================================================================

    private bool BindSceneNodes()
    {
        _backdrop = GetNodeOrNull<ColorRect>("%Backdrop")!;
        _dialog = GetNodeOrNull<PanelContainer>("%Dialog")!;
        _paperTexture = GetNodeOrNull<TextureRect>("%PaperTexture")!;
        _headerIcon = GetNodeOrNull<TextureRect>("%HeaderIcon")!;
        _guTab = GetNodeOrNull<Button>("%GuTab")!;
        _cardTab = GetNodeOrNull<Button>("%CardTab")!;
        _heLianTab = GetNodeOrNull<Button>("%HeLianTab")!;
        _shaZhaoTab = GetNodeOrNull<Button>("%ShaZhaoTab")!;
        _search = GetNodeOrNull<LineEdit>("%RecipeSearch")!;
        _summary = GetNodeOrNull<Label>("%RecipeSummary")!;
        _recipeRows = GetNodeOrNull<VBoxContainer>("%RecipeRows")!;
        _listView = GetNodeOrNull<VBoxContainer>("%RecipeListView")!;

        _cardDetailView = GetNodeOrNull<VBoxContainer>("%CardDetailView")!;
        _cardPortrait = GetNodeOrNull<TextureRect>("%CardPortrait")!;
        _cardDetailTitle = GetNodeOrNull<Label>("%CardDetailTitle")!;
        _cardDetailMeta = GetNodeOrNull<Label>("%CardDetailMeta")!;
        _cardDetailDescription =
            GetNodeOrNull<RichTextLabel>("%CardDetailDescription")!;
        _cardDetailDetailSectionLabel =
            GetNodeOrNull<Label>("%CardDetailDetailSectionLabel")!;
        _cardDetailDetailText =
            GetNodeOrNull<RichTextLabel>("%CardDetailDetailText")!;
        _cardRankButtons = GetNodeOrNull<HFlowContainer>("%CardRankButtons")!;
        _acquisitionContent =
            GetNodeOrNull<VBoxContainer>("%AcquisitionContent")!;
        _heLianPathContent =
            GetNodeOrNull<VBoxContainer>("%HeLianPathContent")!;
        _shaZhaoPathContent =
            GetNodeOrNull<VBoxContainer>("%ShaZhaoPathContent")!;
        _companionContent = GetNodeOrNull<HFlowContainer>("%CompanionContent")!;

        _shaZhaoDetailView =
            GetNodeOrNull<VBoxContainer>("%ShaZhaoDetailView")!;
        _shaZhaoPortrait = GetNodeOrNull<TextureRect>("%ShaZhaoPortrait")!;
        _shaZhaoDetailTitle = GetNodeOrNull<Label>("%ShaZhaoDetailTitle")!;
        _shaZhaoDetailMeta = GetNodeOrNull<Label>("%ShaZhaoDetailMeta")!;
        _shaZhaoDetailDescription =
            GetNodeOrNull<RichTextLabel>("%ShaZhaoDetailDescription")!;
        _shaZhaoDetailDetailSectionLabel =
            GetNodeOrNull<Label>("%ShaZhaoDetailDetailSectionLabel")!;
        _shaZhaoDetailDetailText =
            GetNodeOrNull<RichTextLabel>("%ShaZhaoDetailDetailText")!;
        _shaZhaoMaterialsContent =
            GetNodeOrNull<VBoxContainer>("%ShaZhaoMaterialsContent")!;

        if (_backdrop != null &&
            _dialog != null &&
            _paperTexture != null &&
            _headerIcon != null &&
            _guTab != null &&
            _cardTab != null &&
            _heLianTab != null &&
            _shaZhaoTab != null &&
            _search != null &&
            _summary != null &&
            _recipeRows != null &&
            _listView != null &&
            _cardDetailView != null &&
            _cardPortrait != null &&
            _cardDetailTitle != null &&
            _cardDetailMeta != null &&
            _cardDetailDescription != null &&
            _cardDetailDetailSectionLabel != null &&
            _cardDetailDetailText != null &&
            _cardRankButtons != null &&
            _acquisitionContent != null &&
            _heLianPathContent != null &&
            _shaZhaoPathContent != null &&
            _companionContent != null &&
            _shaZhaoDetailView != null &&
            _shaZhaoPortrait != null &&
            _shaZhaoDetailTitle != null &&
            _shaZhaoDetailMeta != null &&
            _shaZhaoDetailDescription != null &&
            _shaZhaoDetailDetailSectionLabel != null &&
            _shaZhaoDetailDetailText != null &&
            _shaZhaoMaterialsContent != null)
        {
            return true;
        }

        GD.PushError(
            "RecipeCompendiumOverlay：场景节点绑定失败，" +
            "请检查 RecipeCompendium.tscn 是否与脚本中的 %唯一名 一致。"
        );
        return false;
    }

    /// <summary>
    /// 补齐场景无法表达的部分：程序化资源、本地化文案与信号连接。
    ///
    /// 所有静态样式（StyleBox、字体大小、颜色、控件属性）都已写进
    /// <c>RecipeCompendium.tscn</c>，这里不再重复设置，否则会出现两处真相。
    /// </summary>
    private void ApplyTheme()
    {
        _backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        _backdrop.GuiInput += OnBackdropGuiInput;

        // 编辑器预览进程里没有游戏图集加载器，这两项拿不到资源，跳过即可。
        if (!_preview)
        {
            _paperTexture.Texture = CreateXuanPaperTexture();
            _headerIcon.Texture = LoadCompendiumTexture();
        }

        ApplyHeaderWiring();
        ApplyTabsWiring();
        ApplySearchWiring();
        ApplyDetailWiring();
    }

    private void ApplyHeaderWiring()
    {
        GetNodeOrNull<Label>("%TitleLabel")!.Text = T("heading");
        GetNodeOrNull<Label>("%SubtitleLabel")!.Text = T("subtitle");

        Button close = GetNodeOrNull<Button>("%CloseButton")!;
        close.Text = T("close");
        close.TooltipText = T("close.tooltip");
        close.Pressed += CloseDialog;
    }

    private void ApplyTabsWiring()
    {
        _guTab.Text = T("tab.gu");
        _cardTab.Text = T("tab.card");
        _heLianTab.Text = T("tab.heLian");
        _shaZhaoTab.Text = T("tab.shaZhao");

        _guTab.Pressed += () => SelectCategory(RecipeCategory.Gu);
        _cardTab.Pressed += () => SelectCategory(RecipeCategory.Card);
        _heLianTab.Pressed += () => SelectCategory(RecipeCategory.HeLian);
        _shaZhaoTab.Pressed += () => SelectCategory(RecipeCategory.ShaZhao);
    }

    private void ApplySearchWiring()
    {
        _search.TextChanged += _ => RebuildVisibleRows();
        GetNodeOrNull<Label>("%FooterLabel")!.Text = T("footer");
    }

    private void ApplyDetailWiring()
    {
        Button cardBack = GetNodeOrNull<Button>("%CardBackButton")!;
        cardBack.Text = T("back");
        cardBack.Pressed += PopPage;

        Button shaZhaoBack = GetNodeOrNull<Button>("%ShaZhaoBackButton")!;
        shaZhaoBack.Text = T("back");
        shaZhaoBack.Pressed += PopPage;

        GetNodeOrNull<Label>("%CardDetailHintLabel")!.Text = T("detail.hint");
        GetNodeOrNull<Label>("%CardDescriptionSectionLabel")!.Text =
            T("detail.descriptionSection");
        GetNodeOrNull<Label>("%CardDetailDetailSectionLabel")!.Text =
            T("detail.detailSection");
        GetNodeOrNull<Label>("%CardRankSectionLabel")!.Text =
            T("detail.rankSection");
        GetNodeOrNull<Label>("%AcquisitionSectionLabel")!.Text =
            T("detail.acquisitionSection");
        GetNodeOrNull<Label>("%HeLianPathSectionLabel")!.Text =
            T("detail.heLianPathSection");
        GetNodeOrNull<Label>("%ShaZhaoPathSectionLabel")!.Text =
            T("detail.shaZhaoPathSection");
        GetNodeOrNull<Label>("%CompanionSectionLabel")!.Text =
            T("detail.companionSection");
        GetNodeOrNull<Label>("%ShaZhaoDescriptionSectionLabel")!.Text =
            T("detail.descriptionSection");
        GetNodeOrNull<Label>("%ShaZhaoDetailDetailSectionLabel")!.Text =
            T("detail.detailSection");
        GetNodeOrNull<Label>("%ShaZhaoMaterialsSectionLabel")!.Text =
            T("detail.materialsSection");
    }

    // =================================================================
    //  打开 / 关闭
    // =================================================================

    private void OpenDialog()
    {
        if (!IsRunUiAvailable() ||
            _backdrop.Visible ||
            NModalContainer.Instance?.OpenModal != null)
        {
            return;
        }

        try
        {
            LoadCompendiumData();
        }
        catch (Exception exception)
        {
            Warn("读取蛊方大全数据失败：" + exception);
            _guCards = [];
            _allCards = [];
            _recipes = [];
            _cardModels = new Dictionary<Type, CardModel>();
        }

        _search.Text = string.Empty;
        ClearPageStack();
        ShowListPage();
        _backdrop.Visible = true;
        SelectCategory(_selectedCategory);
        _topBarButton?.RefreshOpenState();
        GetViewport().GuiReleaseFocus();
        _search.GrabFocus();
    }

    private void CloseDialog()
    {
        if (!_backdrop.Visible)
        {
            return;
        }

        _backdrop.Visible = false;
        ClearPageStack();
        _topBarButton?.RefreshOpenState();
    }

    private void OnBackdropGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left,
            } mouse)
        {
            return;
        }

        // 点击面板之外才关闭；面板内的点击交给面板自己处理。
        if (!_dialog.GetGlobalRect().HasPoint(mouse.GlobalPosition))
        {
            CloseDialog();
            _backdrop.AcceptEvent();
        }
    }

    private void OnAnchorScanTimerTimeout()
    {
        RefreshTopBarButton();

        // 游戏弹出原生模态框时让出画面，避免两层界面互相遮挡。
        if (_backdrop.Visible && NModalContainer.Instance?.OpenModal != null)
        {
            CloseDialog();
        }
    }

    // =================================================================
    //  页面栈
    // =================================================================

    private void SetPage(Func<Control> page)
    {
        _listView.Visible = false;
        _cardDetailView.Visible = false;
        _shaZhaoDetailView.Visible = false;
        page().Visible = true;
    }

    /// <summary>切换到列表页。列表页同时也是"回退到顶"的目标，因此不进栈。</summary>
    private void ShowListPage() => SetPage(() => _listView);

    /// <summary>压栈并进入新页面。返回按钮与 Esc 会按栈回退。</summary>
    private void PushPage(Func<Control> page)
    {
        _pageStack.Push(page);
        SetPage(page);
    }

    /// <summary>回退一层；栈空时不动（由 Esc/关闭按钮负责收尾）。</summary>
    private void PopPage()
    {
        if (_pageStack.Count > 0)
        {
            _pageStack.Pop();
        }

        SetPage(() => _listView);
    }

    private void ClearPageStack() => _pageStack.Clear();

    // =================================================================
    //  列表页
    // =================================================================

    private void SelectCategory(RecipeCategory category)
    {
        _selectedCategory = category;
        _guTab.ButtonPressed = category == RecipeCategory.Gu;
        _cardTab.ButtonPressed = category == RecipeCategory.Card;
        _heLianTab.ButtonPressed = category == RecipeCategory.HeLian;
        _shaZhaoTab.ButtonPressed = category == RecipeCategory.ShaZhao;

        // 搜索框语义随页签变化：卡牌页按名称查牌，配方页查结果与材料。
        _search.PlaceholderText = category switch
        {
            RecipeCategory.Gu => T("search.gu"),
            RecipeCategory.Card => T("search.card"),
            _ => T("search.recipe"),
        };

        RebuildVisibleRows();
    }

    private void RebuildVisibleRows()
    {
        ClearChildren(_recipeRows);

        string query = SearchQuery;

        if (_selectedCategory == RecipeCategory.Gu)
        {
            RebuildGuRows(query);
            return;
        }

        if (_selectedCategory == RecipeCategory.Card)
        {
            RebuildAllCardRows(query);
            return;
        }

        RebuildRecipeRows(query);
    }

    private string SearchQuery => _search.Text.Trim();

    private void RebuildGuRows(string query)
    {
        // 蛊虫按名称过滤；名称取本地化标题，因此中英文各查各的。
        CardModel[] visible = FilterCardsByName(_guCards, query);

        _summary.Text = query.Length == 0
            ? T("summary.gu", ("Count", _guCards.Count))
            : T(
                "summary.filteredGu",
                ("Shown", visible.Length),
                ("Count", _guCards.Count)
            );

        if (visible.Length == 0)
        {
            _recipeRows.AddChild(BuildEmptyState(
                _guCards.Count == 0 ? "empty.gu" : "empty.noMatch"
            ));
            return;
        }

        AddCardRows(visible);
    }

    /// <summary>
    /// 卡牌大全：收录本模组全部卡池里的每一张牌。
    ///
    /// 除蛊牌外还包括伴生牌、杀招推演这类系统牌以及以后的杀招牌——它们都不在
    /// 蛊牌主池里，只列蛊牌的话这些卡在蛊方大全里就没有任何入口。
    /// </summary>
    private void RebuildAllCardRows(string query)
    {
        CardModel[] visible = FilterCardsByName(_allCards, query);

        _summary.Text = query.Length == 0
            ? T("summary.cards", ("Count", _allCards.Count))
            : T(
                "summary.filteredCards",
                ("Shown", visible.Length),
                ("Count", _allCards.Count)
            );

        if (visible.Length == 0)
        {
            _recipeRows.AddChild(BuildEmptyState(
                _allCards.Count == 0 ? "empty.card" : "empty.noMatch"
            ));
            return;
        }

        AddCardRows(visible);
    }

    /// <summary>按本地化标题过滤卡牌；标题按语言本地化，因此中英文各查各的。</summary>
    private static CardModel[] FilterCardsByName(
        IReadOnlyList<CardModel> cards,
        string query
    ) =>
        cards
            .Where(card =>
                query.Length == 0 ||
                card.Title.Contains(
                    query,
                    StringComparison.CurrentCultureIgnoreCase
                )
            )
            .OrderBy(card => card.Title, StringComparer.CurrentCulture)
            .ToArray();

    private void AddCardRows(IEnumerable<CardModel> cards)
    {
        foreach (CardModel card in cards)
        {
            _recipeRows.AddChild(BuildCardRow(card));
        }
    }

    private void RebuildRecipeRows(string query)
    {
        RecipeViewModel[] visible = _recipes
            .Where(recipe =>
                recipe.Category == _selectedCategory &&
                (query.Length == 0 ||
                 recipe.SearchText.Contains(
                     query,
                     StringComparison.CurrentCultureIgnoreCase
                 ))
            )
            .OrderBy(recipe => recipe.ResultName, StringComparer.CurrentCulture)
            .ThenBy(recipe => recipe.SearchText, StringComparer.CurrentCulture)
            .ToArray();

        int categoryCount = _recipes.Count(recipe =>
            recipe.Category == _selectedCategory
        );

        _summary.Text = query.Length == 0
            ? T("summary.recipes", ("Count", categoryCount))
            : T(
                "summary.filteredRecipes",
                ("Shown", visible.Length),
                ("Count", categoryCount)
            );

        if (visible.Length == 0)
        {
            if (_recipes.Count == 0)
            {
                _recipeRows.AddChild(BuildEmptyState("empty.notReady"));
            }
            else if (categoryCount == 0)
            {
                // 杀招池为空是当前版本的正常状态，给出可行动的说明而不是报错。
                _recipeRows.AddChild(BuildEmptyState(
                    _selectedCategory == RecipeCategory.ShaZhao
                        ? "empty.shaZhao"
                        : "empty.heLian"
                ));
            }
            else
            {
                _recipeRows.AddChild(BuildEmptyState("empty.noMatch"));
            }

            return;
        }

        foreach (RecipeViewModel recipe in visible)
        {
            _recipeRows.AddChild(BuildRecipeRow(recipe));
        }
    }

    private Control BuildEmptyState(string locKeySuffix)
    {
        Label empty = CreateLabel(T(locKeySuffix), 22, Muted);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        empty.CustomMinimumSize = new Vector2(0f, 120f);
        return empty;
    }

    /// <summary>
    /// 卡牌列表行：左侧贴图缩略图，右侧名称 + 摘要。
    /// 蛊牌摘要显示转数与获取方式，其余卡牌（伴生牌 / 系统牌 / 杀招牌）显示费用、稀有度与类型。
    /// </summary>
    private Control BuildCardRow(CardModel card)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        panel.AddThemeStyleboxOverride("panel", CreateRowStyle());

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        HBoxContainer row = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 16);
        margin.AddChild(row);

        // 贴图缩略图：等比缩放居中，不渲染卡框。
        row.AddChild(BuildPortraitThumbnail(card, new Vector2(120f, 92f)));

        VBoxContainer copy = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        copy.AddThemeConstantOverride("separation", 4);
        row.AddChild(copy);

        Label title = CreateLabel(card.Title, 25, Gold);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        copy.AddChild(title);

        copy.AddChild(CreateLabel(
            BuildCardRowSubtitle(card),
            17,
            Cyan
        ));

        // 整行可点：包一层透明按钮，点击范围覆盖整张卡片。
        Button hit = new()
        {
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            TooltipText = T("detail.cardNameTooltip"),
        };
        hit.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hit.Pressed += () => OpenCardDetail(card.GetType());
        panel.AddChild(hit);

        return panel;
    }

    /// <summary>
    /// 列表行摘要：蛊牌走"转数 + 获取方式"，其余卡牌走"费用 · 稀有度 · 类型"。
    /// </summary>
    private string BuildCardRowSubtitle(CardModel card) =>
        card is IGuCard
            ? BuildGuRowSubtitle(card)
            : T(
                "detail.plainMeta",
                ("EnergyCost", card.EnergyCost.Canonical),
                ("Rarity", GetRarityName(card.Rarity)),
                ("Kind", GetKindName(card.Type))
            );

    /// <summary>蛊虫行的副标题：转数与获取方式摘要。</summary>
    private string BuildGuRowSubtitle(CardModel card)
    {
        GuDetail? detail = TryBuildGuDetail(card.GetType(), previewRank: 1);

        if (detail is null)
        {
            return string.Empty;
        }

        List<string> parts =
        [
            T("rankButton", ("Rank", ToChineseRank(detail.GuRank))),
        ];

        parts.AddRange(detail.Acquisitions.Select(kind =>
            DescribeAcquisition(kind, detail.StarterCopyCount)
        ));

        return string.Join(" · ", parts.Where(part => part.Length > 0));
    }

    /// <summary>
    /// 配方列表行：整行单行显示"结果 ← 材料"，末尾附最低转数要求。
    ///
    /// 用 <see cref="HFlowContainer"/> 而不是 HBoxContainer：正常分辨率下全部
    /// 排在一行，只有在极窄窗口放不下时才自动折行，不会把内容挤成零宽或溢出。
    /// </summary>
    private Control BuildRecipeRow(RecipeViewModel recipe)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        panel.AddThemeStyleboxOverride("panel", CreateRowStyle());

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        HFlowContainer line = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        line.AddThemeConstantOverride("h_separation", 6);
        line.AddThemeConstantOverride("v_separation", 6);
        margin.AddChild(line);

        // 结果名可点击 → 打开结果卡的查看页面。
        line.AddChild(CreateCardLink(
            recipe.ResultType,
            recipe.ResultName,
            fontSize: 22
        ));

        line.AddChild(CreateLabel("  ←  ", 20, Cinnabar));

        for (int index = 0; index < recipe.MaterialTypes.Count; index++)
        {
            if (index > 0)
            {
                line.AddChild(CreateLabel(" + ", 20, Muted));
            }

            Type materialType = recipe.MaterialTypes[index];
            line.AddChild(CreateCardLink(
                materialType,
                GuCompendiumModel.TryGetCardName(materialType) ??
                    materialType.Name,
                fontSize: 20
            ));
        }

        if (recipe.MinimumMaterialRank > AbstractGuCard.MinimumGuRank)
        {
            line.AddChild(CreateLabel("   ", 16, Muted));
            line.AddChild(CreateLabel(
                T(
                    "path.rankRequirement",
                    ("Rank", ToChineseRank(recipe.MinimumMaterialRank))
                ),
                17,
                Cyan
            ));
        }

        return panel;
    }

    // =================================================================
    //  卡牌查看页面
    // =================================================================

    /// <summary>
    /// 打开一张卡的查看页面。
    ///
    /// 三类页面共用同一个"卡牌详情"容器：
    /// 1. 蛊牌——贴图 + 转数 + 获取方式 + 合练/杀招路径 + 伴生牌；
    /// 2. 杀招——独立页面（费用走原生能量、组方材料可点）；
    /// 3. 普通牌 / 伴生牌——贴图 + 费用于稀有度类型 + 描述，蛊牌专属分区整块隐藏。
    /// </summary>
    private void OpenCardDetail(Type cardType)
    {
        object? view = TryBuildCardView(cardType);

        if (view is null)
        {
            return;
        }

        if (IsShaZhaoType(cardType) && view is GuDetail shaZhaoDetail)
        {
            PushPage(() => _shaZhaoDetailView);
            PopulateShaZhaoDetail(shaZhaoDetail);
            return;
        }

        PushPage(() => _cardDetailView);

        if (view is GuDetail guDetail)
        {
            _selectedCardType = cardType;
            _selectedRank = Math.Max(
                AbstractGuCard.MinimumGuRank,
                guDetail.GuRank
            );
            SetGuSectionsVisible(true);
            BuildRankButtons(guDetail);
            PopulateCardDetail(guDetail);
            return;
        }

        if (view is PlainCardDetail plainDetail)
        {
            _selectedCardType = null;
            SetGuSectionsVisible(false);
            PopulatePlainCardDetail(plainDetail);
        }
    }

    /// <summary>
    /// 蛊牌专属分区（预览转数 / 获取方式 / 合练路径 / 杀招路径 / 伴生牌）的显隐。
    /// 普通牌走同一个容器，但没有任何一项可用，必须整块隐藏，否则会留下空标题。
    /// </summary>
    private void SetGuSectionsVisible(bool visible)
    {
        foreach (string name in new[]
                 {
                     "%CardRankSectionLabel",
                     "%CardRankButtons",
                     "%AcquisitionSectionLabel",
                     "%AcquisitionContent",
                     "%HeLianPathSectionLabel",
                     "%HeLianPathContent",
                     "%ShaZhaoPathSectionLabel",
                     "%ShaZhaoPathContent",
                     "%CompanionSectionLabel",
                     "%CompanionContent",
                 })
        {
            if (GetNodeOrNull<Control>(name) is { } node)
            {
                node.Visible = visible;
            }
        }
    }

    /// <summary>普通牌 / 伴生牌的查看页面：只保留共有的贴图、元信息与描述。</summary>
    private void PopulatePlainCardDetail(PlainCardDetail detail)
    {
        _cardPortrait.Texture = TryGetCardPortrait(detail.CardType);
        _cardDetailTitle.Text = detail.Name;
        _cardDetailMeta.Text = T(
            "detail.plainMeta",
            ("EnergyCost", detail.EnergyCost),
            ("Rarity", GetRarityName(detail.Rarity)),
            ("Kind", GetKindName(detail.Kind))
        );
        _cardDetailDescription.Text =
            FormatDescriptionBbcode(detail.Description);
        SetDetailSection(
            _cardDetailDetailSectionLabel,
            _cardDetailDetailText,
            detail.DetailDescription
        );
    }

    private static bool IsShaZhaoType(Type cardType) =>
        typeof(AbstractShaZhaoCard).IsAssignableFrom(cardType);

    /// <summary>
    /// 填充"详细说明"分区。
    ///
    /// 详细说明是蛊方大全专用的最详细文案（.detailDescription），卡面只放精简版；
    /// 没有配置详细说明的卡整块隐藏，避免出现空标题或重复卡面文本。
    /// </summary>
    private static void SetDetailSection(
        Label sectionLabel,
        RichTextLabel textLabel,
        string? detailDescription
    )
    {
        bool hasText = !string.IsNullOrWhiteSpace(detailDescription);

        sectionLabel.Visible = hasText;
        textLabel.Visible = hasText;
        textLabel.Text = hasText
            ? FormatDescriptionBbcode(detailDescription!)
            : string.Empty;
    }

    private void PopulateCardDetail(GuDetail detail)
    {
        _cardPortrait.Texture = TryGetCardPortrait(detail.CardType);
        _cardDetailTitle.Text = detail.Name;
        _cardDetailMeta.Text = BuildGuMetaText(detail);
        _cardDetailDescription.Text =
            FormatDescriptionBbcode(detail.Description);
        SetDetailSection(
            _cardDetailDetailSectionLabel,
            _cardDetailDetailText,
            detail.DetailDescription
        );

        BuildAcquisitionSection(detail);
        BuildPathSection(_heLianPathContent, detail, isShaZhao: false);
        BuildPathSection(_shaZhaoPathContent, detail, isShaZhao: true);
        BuildCompanionSection(detail);
    }

    private void PopulateShaZhaoDetail(GuDetail detail)
    {
        _shaZhaoPortrait.Texture = TryGetCardPortrait(detail.CardType);
        _shaZhaoDetailTitle.Text = detail.Name;

        // 杀招费用是原生能量，取自它的第一条配方（同一条杀招的所有配方费用一致）。
        int energyCost = detail.ProducedBy
            .Select(link => link.CostEnergy)
            .DefaultIfEmpty(0)
            .Max();

        _shaZhaoDetailMeta.Text = energyCost > 0
            ? T(
                "detail.shaZhaoMeta",
                ("Rank", ToChineseRank(detail.GuRank)),
                ("Rarity", GetRarityName(detail.Rarity)),
                ("Kind", GetKindName(detail.Kind)),
                ("EnergyCost", energyCost),
                ("MaxUses", detail.MaxUses)
            )
            : T(
                "detail.metaNoCooldown",
                ("Rank", ToChineseRank(detail.GuRank)),
                ("Rarity", GetRarityName(detail.Rarity)),
                ("Kind", GetKindName(detail.Kind))
            );

        _shaZhaoDetailDescription.Text =
            FormatDescriptionBbcode(detail.Description);
        SetDetailSection(
            _shaZhaoDetailDetailSectionLabel,
            _shaZhaoDetailDetailText,
            detail.DetailDescription
        );

        ClearChildren(_shaZhaoMaterialsContent);

        if (detail.ProducedBy.Count == 0)
        {
            _shaZhaoMaterialsContent.AddChild(
                CreateLabel(T("detail.relatedEmpty"), 18, Muted)
            );
            return;
        }

        foreach (GuRecipeLink link in detail.ProducedBy)
        {
            _shaZhaoMaterialsContent.AddChild(BuildMaterialFormula(link));
        }
    }

    /// <summary>材料公式行：每个材料一个可点链接。</summary>
    private Control BuildMaterialFormula(GuRecipeLink link)
    {
        HFlowContainer formula = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        formula.AddThemeConstantOverride("h_separation", 5);
        formula.AddThemeConstantOverride("v_separation", 6);

        for (int index = 0; index < link.MaterialTypes.Count; index++)
        {
            if (index > 0)
            {
                formula.AddChild(CreateLabel(" + ", 20, Muted));
            }

            Type materialType = link.MaterialTypes[index];
            formula.AddChild(CreateCardLink(
                materialType,
                GuCompendiumModel.TryGetCardName(materialType) ??
                    materialType.Name,
                fontSize: 20
            ));
        }

        formula.AddChild(CreateLabel("  →  ", 20, Cinnabar));
        formula.AddChild(CreateCardLink(
            link.ResultType,
            link.ResultName,
            fontSize: 20
        ));

        return formula;
    }

    private void BuildRankButtons(GuDetail detail)
    {
        ClearChildren(_cardRankButtons);

        int maxRank = AbstractGuCard.MinimumGuRank;

        if (GuCardCatalog.TryFindCanonical(
                detail.CardType,
                out CardModel? canonical
            ) &&
            canonical is AbstractGuCard guCard)
        {
            maxRank = Math.Max(maxRank, guCard.MaxGuRank);
        }

        for (int rank = AbstractGuCard.MinimumGuRank; rank <= maxRank; rank++)
        {
            int selected = rank;
            Button rankButton = CreateInkButton(
                T("rankButton", ("Rank", ToChineseRank(rank)))
            );
            rankButton.ToggleMode = true;
            rankButton.ButtonPressed = rank == _selectedRank;
            rankButton.Pressed += () => SelectPreviewRank(selected);
            _cardRankButtons.AddChild(rankButton);
        }
    }

    private void SelectPreviewRank(int rank)
    {
        _selectedRank = rank;

        int currentRank = AbstractGuCard.MinimumGuRank;

        foreach (Node child in _cardRankButtons.GetChildren())
        {
            if (child is Button button)
            {
                button.ButtonPressed = currentRank++ == rank;
            }
        }

        if (_selectedCardType is { } cardType &&
            TryBuildGuDetail(cardType, rank) is { } detail)
        {
            _cardDetailMeta.Text = BuildGuMetaText(detail);
            _cardDetailDescription.Text =
                FormatDescriptionBbcode(detail.Description);
        }
    }

    private string BuildGuMetaText(GuDetail detail)
    {
        if (detail.RecoveryDelayTurns <= 0)
        {
            return T(
                "detail.metaNoCooldown",
                ("Rank", ToChineseRank(detail.GuRank)),
                ("Rarity", GetRarityName(detail.Rarity)),
                ("Kind", GetKindName(detail.Kind))
            );
        }

        return T(
            "detail.meta",
            ("Rank", ToChineseRank(detail.GuRank)),
            ("Rarity", GetRarityName(detail.Rarity)),
            ("Kind", GetKindName(detail.Kind)),
            ("YuanQiCost", detail.YuanQiCost),
            ("MaxUses", detail.MaxUses),
            ("RecoveryTurns", detail.RecoveryDelayTurns)
        );
    }

    // =================================================================
    //  详情页各分区
    // =================================================================

    private void BuildAcquisitionSection(GuDetail detail)
    {
        ClearChildren(_acquisitionContent);

        bool any = false;

        foreach (GuAcquisitionKind kind in detail.Acquisitions)
        {
            string text = kind switch
            {
                GuAcquisitionKind.StarterDeck => T(
                    "acquisition.starterDeck",
                    ("Count", detail.StarterCopyCount)
                ),
                GuAcquisitionKind.CardReward => T("acquisition.cardReward"),
                GuAcquisitionKind.HeLianOnly => T("acquisition.heLianOnly"),
                _ => string.Empty,
            };

            if (text.Length == 0)
            {
                continue;
            }

            _acquisitionContent.AddChild(BuildBullet(text, Ink));
            any = true;
        }

        foreach (string extraKey in detail.ExtraAcquisitionKeys)
        {
            _acquisitionContent.AddChild(
                BuildBullet(T(extraKey), Ink)
            );
            any = true;
        }

        if (!any)
        {
            _acquisitionContent.AddChild(
                CreateLabel(T("detail.relatedEmpty"), 18, Muted)
            );
        }

        // 带伴生牌的蛊被当合练材料消耗时会连带失去伴生牌，这里明确警示。
        if (detail.Companions.Count > 0 &&
            detail.UsedAsHeLianMaterial.Count > 0)
        {
            Label warning = CreateLabel(
                T("acquisition.companionWarning"),
                17,
                Cinnabar
            );
            warning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _acquisitionContent.AddChild(warning);
        }
    }

    private Control BuildBullet(string text, Color color)
    {
        Label label = CreateLabel("· " + text, 19, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private void BuildPathSection(
        VBoxContainer host,
        GuDetail detail,
        bool isShaZhao
    )
    {
        ClearChildren(host);

        IReadOnlyList<GuRecipeLink> produced = isShaZhao
            ? []
            : detail.ProducedBy;
        IReadOnlyList<GuRecipeLink> usedAsMaterial = isShaZhao
            ? detail.UsedAsShaZhaoMaterial
            : detail.UsedAsHeLianMaterial;

        // 产出：哪些配方能造出这张卡。
        if (produced.Count > 0)
        {
            host.AddChild(CreateLabel(T("path.producedBy"), 17, Cinnabar));

            foreach (GuRecipeLink link in produced)
            {
                host.AddChild(BuildMaterialFormula(link));
            }
        }

        // 作为材料：这张卡能参与哪些配方。
        if (usedAsMaterial.Count > 0)
        {
            host.AddChild(CreateLabel(
                T("path.usedAsMaterial"),
                17,
                Cinnabar
            ));

            foreach (GuRecipeLink link in usedAsMaterial)
            {
                host.AddChild(BuildMaterialFormula(link));
            }
        }

        if (produced.Count == 0 && usedAsMaterial.Count == 0)
        {
            host.AddChild(CreateLabel(T("detail.relatedEmpty"), 18, Muted));
        }
    }

    private void BuildCompanionSection(GuDetail detail)
    {
        ClearChildren(_companionContent);

        if (detail.Companions.Count == 0)
        {
            _companionContent.AddChild(
                CreateLabel(T("detail.relatedEmpty"), 18, Muted)
            );
            return;
        }

        // 与合练配方页一致：伴生牌就是一个可点击的立体链接按钮，
        // 不再额外画贴图小块（用户要求统一为配方页的按钮观感）。
        // 一只蛊可以带多张不同伴生，逐张列出。
        foreach (GuCompanionLink companion in detail.Companions)
        {
            _companionContent.AddChild(CreateCardLink(
                companion.CardType,
                companion.Name,
                fontSize: 20
            ));
        }
    }

    /// <summary>
    /// 卡牌入口按钮：点击进入该卡的查看页面。
    ///
    /// 蛊牌、杀招、普通牌与伴生牌现在都有查看页面，因此按钮一律可点；
    /// 区别只在页面内容——普通牌没有转数 / 获取方式 / 合练与杀招路径 / 伴生牌分区。
    /// </summary>
    private Button CreateCardLink(Type cardType, string name, int fontSize)
    {
        Button button = CreateInkButton(name);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.TooltipText = T("detail.cardNameTooltip");
        button.Pressed += () => OpenCardDetail(cardType);
        return button;
    }

    private Texture2D? TryGetCardPortrait(Type cardType)
    {
        if (!GuCardCatalog.TryFindCanonical(cardType, out CardModel? canonical))
        {
            ShaZhaoCardCatalog.TryFindCanonical(cardType, out canonical);
        }

        return canonical is null
            ? null
            : GuPortraitCatalog.TryGetPortrait(canonical);
    }

    /// <summary>列表行的贴图缩略图，等比缩放居中。</summary>
    private Control BuildPortraitThumbnail(CardModel card, Vector2 size)
    {
        CenterContainer host = new()
        {
            CustomMinimumSize = size,
        };

        Texture2D? texture = GuPortraitCatalog.TryGetPortrait(card);

        if (texture != null)
        {
            TextureRect portrait = new()
            {
                Texture = texture,
                CustomMinimumSize = size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            host.AddChild(portrait);
        }

        return host;
    }

    // =================================================================
    //  数据装载
    // =================================================================

    private void LoadCompendiumData()
    {
        _guCards = GuCompendiumModel.GetAllGuCards();
        _allCards = GuCompendiumModel.GetAllCards();

        // 蛊牌、伴生牌、系统牌与杀招牌分别来自三个卡池，合练结果蛊在蛊牌池里。
        // 这里只用于"按类型取名称"的兜底查找，界面主体走 GuCompendiumModel。
        _cardModels = _allCards
            .GroupBy(card => card.GetType())
            .ToDictionary(group => group.Key, group => group.First());

        _recipes = BuildRecipeViewModels(_cardModels);
        LogEnergyIconDiagnostics();
    }

    /// <summary>
    /// 数据装载后记一条元气图标自检日志。
    ///
    /// 卡牌左上角显示哪个图标，取决于 RitsuLib 的
    /// <c>IModCardAssetOverrides.CustomEnergyIconPath</c>（卡片级）与
    /// <c>IModBigEnergyIconPool.BigEnergyIconPath</c>（卡池级）两条覆盖。
    /// 这两条一旦有哪边没接上，卡片会退回"原版图集里不存在的
    /// energy_guzhenrenrubild.tres"，表现为图标缺失，而界面本身不会报错。
    /// 因此这里主动把实际解析结果打出来，方便一眼定位是哪一层没生效。
    /// </summary>
    private static void LogEnergyIconDiagnostics()
    {
        try
        {
            if (GuCompendiumModel.GetAllGuCards().FirstOrDefault() is not { } card)
            {
                return;
            }

            Texture2D? resolved = card.EnergyIcon;
            string resolvedPath = resolved?.ResourcePath ?? "(null)";

            // CustomEnergyIconPath 定义在 RitsuLib 的 IModCardAssetOverrides 上，
            // 不在 CardModel 上，因此要么是 ModCardTemplate 子类要么显式转接口。
            string customPath =
                card is STS2RitsuLib.Scaffolding.Content.Patches
                    .IModCardAssetOverrides overrides
                    ? overrides.CustomEnergyIconPath ?? "(null)"
                    : "(not IModCardAssetOverrides)";

            Entry.Logger.Info(
                "[元气图标自检] " +
                $"card={card.Id} " +
                $"customCardPath={customPath} " +
                $"resolvedEnergyIcon={resolvedPath} " +
                $"largePath={Combat.YuanQiSystem.LargeIconPath} " +
                $"smallPath={Combat.YuanQiSystem.SmallIconPath}"
            );
        }
        catch (Exception exception)
        {
            Warn("元气图标自检失败：" + exception.Message);
        }
    }

    private static IReadOnlyList<RecipeViewModel> BuildRecipeViewModels(
        IReadOnlyDictionary<Type, CardModel> cards
    )
    {
        string NameOf(Type type) =>
            cards.TryGetValue(type, out CardModel? card)
                ? card.Title
                : type.Name;

        List<RecipeViewModel> recipes = [];

        foreach ((
                     Type resultType,
                     IReadOnlyList<Type> materials,
                     int minimumRank
                 ) in ShaZhaoRecipeRegistry.GetRecipeDetails())
        {
            string resultName = NameOf(resultType);
            recipes.Add(new RecipeViewModel(
                RecipeCategory.ShaZhao,
                resultName,
                resultType,
                materials,
                minimumRank,
                $"{resultName} {FormatMaterials(materials, NameOf)}"
            ));
        }

        foreach ((
                     Type resultType,
                     IReadOnlyList<Type> materials,
                     int minimumRank
                 ) in HeLianRecipeRegistry.GetRecipeDetails())
        {
            string resultName = NameOf(resultType);
            recipes.Add(new RecipeViewModel(
                RecipeCategory.HeLian,
                resultName,
                resultType,
                materials,
                minimumRank,
                $"{resultName} {FormatMaterials(materials, NameOf)} {minimumRank}"
            ));
        }

        return recipes;
    }

    /// <summary>
    /// 组装一张卡的查看页数据。任何异常都收敛成 null + 一条日志：
    /// 详情页是只读展示，不该因为一张卡的数据问题把整个界面打断。
    /// </summary>
    private static object? TryBuildCardView(Type cardType)
    {
        try
        {
            return GuCompendiumModel.TryBuildCardView(
                cardType,
                AbstractGuCard.MinimumGuRank
            );
        }
        catch (Exception exception)
        {
            Warn($"蛊方大全：组装 {cardType.Name} 详情失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>只要蛊牌详情；调用方明确知道自己面对的是蛊牌时用它。</summary>
    private static GuDetail? TryBuildGuDetail(Type cardType, int previewRank)
    {
        try
        {
            return GuCompendiumModel.BuildDetail(cardType, previewRank);
        }
        catch (Exception exception)
        {
            Warn($"蛊方大全：组装 {cardType.Name} 详情失败：{exception.Message}");
            return null;
        }
    }

    private static string FormatMaterials(
        IEnumerable<Type> materialTypes,
        Func<Type, string> nameOf
    )
    {
        return string.Join(
            " + ",
            materialTypes
                .GroupBy(type => type)
                .OrderBy(
                    group => nameOf(group.Key),
                    StringComparer.CurrentCulture
                )
                .Select(group =>
                {
                    string name = nameOf(group.Key);
                    int count = group.Count();
                    return count == 1 ? name : $"{name} ×{count}";
                })
        );
    }

    /// <summary>把获取方式枚举翻成一句可读文案。</summary>
    private string DescribeAcquisition(
        GuAcquisitionKind kind,
        int starterCopyCount
    ) => kind switch
    {
        GuAcquisitionKind.StarterDeck => starterCopyCount > 0
            ? T("acquisition.starterDeck", ("Count", starterCopyCount))
            : T("acquisition.cardReward"),
        GuAcquisitionKind.CardReward => T("acquisition.cardReward"),
        GuAcquisitionKind.HeLianOnly => T("acquisition.heLianOnly"),
        _ => string.Empty,
    };

    // =================================================================
    //  顶栏按钮
    // =================================================================

    private void RefreshTopBarButton()
    {
        if (!IsRunUiAvailable())
        {
            RemoveTopBarButton();

            if (_backdrop.Visible)
            {
                CloseDialog();
            }

            return;
        }

        NTopBarMapButton? mapButton = NRun.Instance?.GlobalUi?.TopBar?.Map;

        if (mapButton == null ||
            !GodotObject.IsInstanceValid(mapButton) ||
            mapButton.GetParent() == null)
        {
            RemoveTopBarButton();
            return;
        }

        // 顶栏未重建、锚点仍是同一个原生按钮时只同步可见性。
        if (_topBarButton != null &&
            GodotObject.IsInstanceValid(_topBarButton) &&
            _mapButton == mapButton &&
            _topBarButton.GetParent() == mapButton.GetParent())
        {
            _topBarButton.Visible = mapButton.Visible;
            return;
        }

        RemoveTopBarButton();
        AttachTopBarButton(mapButton);
    }

    private void AttachTopBarButton(NTopBarMapButton mapButton)
    {
        Node? parent = mapButton.GetParent();

        if (parent == null)
        {
            return;
        }

        RecipeCompendiumTopBarButton button;

        try
        {
            button = RecipeCompendiumTopBarButton.Create(mapButton, this);
        }
        catch (Exception exception)
        {
            Warn("创建原生顶栏蛊方大全按钮失败：" + exception);
            return;
        }

        // 紧贴小地图按钮左侧插入，并接管它的左右焦点邻居。
        int mapIndex = mapButton.GetIndex();
        parent.AddChild(button);
        parent.MoveChild(button, mapIndex);

        CopyMapButtonLayout(
            mapButton,
            button,
            parent is Container
                ? 0f
                : -(mapButton.Size.X + TopBarButtonGap)
        );

        _originalMapFocusNeighborLeft = mapButton.FocusNeighborLeft;
        button.FocusNeighborLeft = _originalMapFocusNeighborLeft;
        button.FocusNeighborRight = mapButton.GetPath();
        button.FocusNeighborTop = mapButton.FocusNeighborTop;
        button.FocusNeighborBottom = mapButton.FocusNeighborBottom;
        mapButton.FocusNeighborLeft = button.GetPath();

        _mapButton = mapButton;
        _topBarButton = button;
    }

    private static void CopyMapButtonLayout(
        NTopBarMapButton source,
        RecipeCompendiumTopBarButton target,
        float horizontalOffset
    )
    {
        target.AnchorLeft = source.AnchorLeft;
        target.AnchorTop = source.AnchorTop;
        target.AnchorRight = source.AnchorRight;
        target.AnchorBottom = source.AnchorBottom;
        target.OffsetLeft = source.OffsetLeft + horizontalOffset;
        target.OffsetTop = source.OffsetTop;
        target.OffsetRight = source.OffsetRight + horizontalOffset;
        target.OffsetBottom = source.OffsetBottom;
        target.CustomMinimumSize = source.CustomMinimumSize;
        target.SizeFlagsHorizontal = source.SizeFlagsHorizontal;
        target.SizeFlagsVertical = source.SizeFlagsVertical;
        target.GrowHorizontal = source.GrowHorizontal;
        target.GrowVertical = source.GrowVertical;
        target.PivotOffset = source.PivotOffset;
        target.ZIndex = source.ZIndex;
        target.Visible = source.Visible;
    }

    private void RemoveTopBarButton()
    {
        RecipeCompendiumTopBarButton? button = _topBarButton;
        NTopBarMapButton? mapButton = _mapButton;

        _topBarButton = null;
        _mapButton = null;

        // 只还原仍指向本按钮的焦点邻居，避免覆盖顶栏重建后的新连接。
        if (button != null &&
            GodotObject.IsInstanceValid(button) &&
            mapButton != null &&
            GodotObject.IsInstanceValid(mapButton) &&
            mapButton.FocusNeighborLeft == button.GetPath())
        {
            mapButton.FocusNeighborLeft = _originalMapFocusNeighborLeft;
        }

        if (button != null && GodotObject.IsInstanceValid(button))
        {
            button.QueueFreeSafely();
        }

        _originalMapFocusNeighborLeft = new NodePath();
    }

    private static bool IsRunUiAvailable()
    {
        RunManager runManager = RunManager.Instance;
        return runManager.IsInProgress && !runManager.IsCleaningUp;
    }

    // =================================================================
    //  编辑器预览
    // =================================================================

    /// <summary>
    /// 编辑器专用：显示底图并塞一份示例，让场景打开即见成品版式。
    ///
    /// 这里刻意不触碰 <c>ModelDb</c>、注册表与图集资源：它们在普通编辑器进程里
    /// 都不可用。示例条目的目的是让四个页签、搜索框、列表行、页脚各占其位。
    /// </summary>
    private void ShowPreviewContent()
    {
        // 本类在编辑器里会往场景树塞示例节点，那会让编辑器把场景标记为已修改，
        // 用户顺手 Ctrl+S 就会把预览内容写进真正的 .tscn。
        // _edit_lock_ 是 Godot 编辑器自用的只读锁元数据：置位后编辑器不会把
        // 该场景标脏，也不会提示保存。界面本来就完全由脚本驱动，锁掉编辑无损失。
        SetMeta("_edit_lock_", true);

        _backdrop.Visible = true;
        _summary.Text = T("summary.gu", ("Count", 0));

        _recipeRows.AddChild(CreateLabel(
            "（编辑器预览示例，游戏内为真实蛊虫与配方）",
            16,
            Muted
        ));
        _recipeRows.AddChild(BuildPreviewRow("玉皮蛊", "一转 · 角色初始牌组 · 卡牌奖励"));
        _recipeRows.AddChild(BuildPreviewRow("聚光蛊", "只能通过合练获得"));

        _guTab.ButtonPressed = true;
        _cardTab.ButtonPressed = false;
        _heLianTab.ButtonPressed = false;
        _shaZhaoTab.ButtonPressed = false;
        _listView.Visible = true;
    }

    private Control BuildPreviewRow(string title, string subtitle)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", CreateRowStyle());

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        VBoxContainer contents = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        contents.AddThemeConstantOverride("separation", 6);
        margin.AddChild(contents);
        contents.AddChild(CreateLabel(title, 25, Gold));
        contents.AddChild(CreateLabel(subtitle, 17, Cyan));
        return panel;
    }

    // =================================================================
    //  程序化资源
    // =================================================================

    /// <summary>
    /// 动态行样式：列表行是运行时按数据创建的，无法写进场景。
    ///
    /// 数值必须与 <c>RecipeCompendium.tscn</c> 里 <c>StyleRow</c> 子资源保持一致
    /// （场景内部子资源脚本取不到，只能两边同值）；改动其中一边时记得同步另一边。
    /// </summary>
    private static StyleBoxFlat CreateRowStyle() => new()
    {
        BgColor = RowColor,
        BorderColor = new Color("9a8062"),
        BorderWidthLeft = 2,
        BorderWidthTop = 1,
        BorderWidthRight = 2,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
    };

    /// <summary>
    /// 程序化生成宣纸底纹：一层随机纤维颗粒叠加一层竖向纸纹，平铺在面板上。
    /// 不依赖外部图片资源，避免为一张底纹新增二进制资源。
    /// </summary>
    private static Texture2D CreateXuanPaperTexture()
    {
        const int Size = 192;
        Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        Color paper = new("172126");
        Color fiber = new("80715b");

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                uint hash = ((uint)x * 374761393u) + ((uint)y * 668265263u);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                float grain = (hash & 255) / 255f;
                float strand = MathF.Abs(MathF.Sin((y * 0.31f) + (x * 0.027f)));
                float amount = grain > 0.972f
                    ? 0.12f
                    : strand > 0.994f
                        ? 0.055f
                        : 0.009f * grain;

                image.SetPixel(x, y, paper.Lerp(fiber, amount));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    // =================================================================
    //  小工具
    // =================================================================

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button CreateInkButton(string text)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(92f, 40f),
        };
        StyleInkButton(button);
        return button;
    }

    /// <summary>
    /// 动态小按钮（卡牌链接 / 转数 / 伴生牌）的立体样式。
    ///
    /// 立体感由三件事构成：受光方向一致的斜角边框（上左亮、下右暗）、
    /// 底部加厚的暗边当作"立面"，以及向下偏移的投影把按钮从面板上"抬起来"。
    ///
    /// 这些按钮都是运行时按数据创建的，无法写进场景，因此样式只能留在代码里；
    /// 场景里的静态按钮（页签、返回、关闭）仍用 .tscn 内自带的样式。
    /// </summary>
    private static StyleBoxFlat CreateSmallButtonStyle(bool highlighted) => new()
    {
        BgColor = highlighted ? new Color("3c5259") : new Color("26343a"),

        // 斜角：上/左亮色、下/右暗色，且底部边框更厚，形成"侧面"。
        BorderColor = highlighted ? Gold : new Color("7d6f5b"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,

        // 向下偏移的投影让按钮与面板分离，强化凸起感。
        ShadowColor = new Color(0f, 0f, 0f, 0.5f),
        ShadowSize = 4,
        ShadowOffset = new Vector2(0f, 2f),

        // 内边距：给"立面"留出空间，文字不会贴到边框上。
        ContentMarginLeft = 12f,
        ContentMarginRight = 12f,
        ContentMarginTop = 6f,
        ContentMarginBottom = 7f,
    };

    private static void StyleInkButton(Button button, int fontSize = 19)
    {
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_pressed_color", Cinnabar);

        button.AddThemeStyleboxOverride("normal", CreateSmallButtonStyle(false));
        button.AddThemeStyleboxOverride("hover", CreateSmallButtonStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateSmallButtonStyle(true));
        button.AddThemeStyleboxOverride("focus", CreateSmallButtonStyle(true));
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFreeSafely();
        }
    }

    /// <summary>
    /// 把卡牌描述里的原生标签翻译成 RichTextLabel 支持的 BBCode。
    /// 蛊牌描述本身由卡牌本地化表提供，这里只做标签映射，不改写文案。
    /// </summary>
    private static string FormatDescriptionBbcode(string text) => text
        .Replace("[gold]", "[color=#d0a45e]", StringComparison.Ordinal)
        .Replace("[/gold]", "[/color]", StringComparison.Ordinal)
        .Replace("[blue]", "[color=#69a6a8]", StringComparison.Ordinal)
        .Replace("[/blue]", "[/color]", StringComparison.Ordinal)
        .Replace("[pink]", "[color=#d58aaa]", StringComparison.Ordinal)
        .Replace("[/pink]", "[/color]", StringComparison.Ordinal)
        .Replace("[purple]", "[color=#aa8bc4]", StringComparison.Ordinal)
        .Replace("[/purple]", "[/color]", StringComparison.Ordinal)
        .Replace("[red]", "[color=#d1584e]", StringComparison.Ordinal)
        .Replace("[/red]", "[/color]", StringComparison.Ordinal)
        .Replace("[orange]", "[color=#e08a3c]", StringComparison.Ordinal)
        .Replace("[/orange]", "[/color]", StringComparison.Ordinal)
        .Replace("[green]", "[color=#7fb069]", StringComparison.Ordinal)
        .Replace("[/green]", "[/color]", StringComparison.Ordinal)
        .Replace("[sine]", string.Empty, StringComparison.Ordinal)
        .Replace("[/sine]", string.Empty, StringComparison.Ordinal);

    private static string GetRarityName(CardRarity rarity) => rarity switch
    {
        CardRarity.Basic => T("rarity.basic"),
        CardRarity.Common => T("rarity.common"),
        CardRarity.Uncommon => T("rarity.uncommon"),
        CardRarity.Rare => T("rarity.rare"),
        _ => rarity.ToString(),
    };

    private static string GetKindName(CardType kind) => kind switch
    {
        CardType.Attack => T("kind.attack"),
        CardType.Skill => T("kind.skill"),
        CardType.Power => T("kind.power"),
        _ => kind.ToString(),
    };

    /// <summary>
    /// 是否使用内置中文，而不是查本地化表。
    ///
    /// 编辑器预览进程里没有 <c>LocManager</c>，必须整段跳过本地化；
    /// 单独的 <see cref="T(string)"/> 无法判空，因此这里统一走静态标记。
    /// </summary>
    private static bool UseBuiltInText { get; set; }

    /// <summary>
    /// 内置中文文案：编辑器预览与本地化缺键时的唯一退路。
    ///
    /// 键段与 <c>localization/zhs/card_library.json</c> 中
    /// <c>GU_ZHEN_REN_RUBILD_COMPENDIUM.*</c> 一一对应，改文案时两边都要动。
    /// 占位符用 <c>{Name}</c>，与 JSON 里的写法一致。
    /// </summary>
    private static readonly Dictionary<string, string> BuiltInChineseText =
        new(StringComparer.Ordinal)
        {
            ["heading"] = "蛊方大全",
            ["subtitle"] = "蛊虫、卡牌、合练与杀招推演的完整图鉴",
            ["close"] = "返回",
            ["close.tooltip"] = "关闭",
            ["tab.gu"] = "蛊虫大全",
            ["tab.card"] = "卡牌大全",
            ["tab.heLian"] = "合练配方",
            ["tab.shaZhao"] = "杀招配方",
            ["search.gu"] = "输入蛊虫名称搜索……",
            ["search.card"] = "输入卡牌名称搜索……",
            ["search.recipe"] = "搜索结果或材料……",
            ["footer"] = "内容直接来自卡池与配方注册表，新增卡牌或杀招会自动收录。",
            ["back"] = "← 返回",
            ["detail.hint"] = "选择转数，说明将同步变化",
            ["detail.descriptionSection"] = "蛊虫介绍",
            ["detail.detailSection"] = "详细说明",
            ["detail.rankSection"] = "预览转数",
            ["detail.acquisitionSection"] = "获取方式",
            ["detail.heLianPathSection"] = "合练路径",
            ["detail.shaZhaoPathSection"] = "杀招路径",
            ["detail.companionSection"] = "伴生牌",
            ["detail.materialsSection"] = "组方材料",
            ["detail.relatedEmpty"] = "暂无。",
            ["detail.cardNameTooltip"] = "查看蛊虫详情",
            ["detail.viewRecipeTooltip"] = "查看配方详情",
            ["rankButton"] = "{Rank}转",
            ["detail.meta"] =
                "{Rank}转 · {Rarity} · {Kind} · 催动消耗 {YuanQiCost} 元气 · " +
                "{MaxUses} 次后冷却 {RecoveryTurns} 回合",
            ["detail.metaNoCooldown"] = "{Rank}转 · {Rarity} · {Kind}",
            ["detail.plainMeta"] = "{EnergyCost} 费 · {Rarity} · {Kind}",
            ["detail.shaZhaoMeta"] =
                "{Rank}转 · {Rarity} · {Kind} · {EnergyCost} 费 · {MaxUses} 次使用",
            ["kind.attack"] = "攻击",
            ["kind.skill"] = "技能",
            ["kind.power"] = "能力",
            ["rarity.basic"] = "基础",
            ["rarity.common"] = "普通",
            ["rarity.uncommon"] = "罕见",
            ["rarity.rare"] = "稀有",
            ["acquisition.starterDeck"] = "角色初始牌组 ×{Count}",
            ["acquisition.cardReward"] = "卡牌奖励",
            ["acquisition.heLianOnly"] = "只能通过合练获得",
            ["acquisition.companionWarning"] =
                "作为合练材料被消耗时，会同时失去它的伴生牌。",
            ["path.producedBy"] = "产出",
            ["path.usedAsMaterial"] = "作为材料",
            ["path.rankRequirement"] = "每张材料至少 {Rank} 转",
            ["path.formula"] = "{Materials} → {Result}",
            ["summary.recipes"] = "共收录 {Count} 条配方",
            ["summary.filteredRecipes"] = "找到 {Shown} / {Count} 条配方",
            ["summary.gu"] = "共收录 {Count} 只蛊虫",
            ["summary.filteredGu"] = "找到 {Shown} / {Count} 只蛊虫",
            ["summary.cards"] = "共收录 {Count} 张卡牌",
            ["summary.filteredCards"] = "找到 {Shown} / {Count} 张卡牌",
            ["empty.notReady"] = "数据尚未就绪，请稍后重新打开。",
            ["empty.shaZhao"] =
                "尚未收录杀招配方。加入带 [ShaZhaoRecipe] 的杀招牌后会自动出现在这里。",
            ["empty.heLian"] = "尚未收录合练配方。",
            ["empty.gu"] = "尚未收录蛊虫。",
            ["empty.card"] = "尚未收录卡牌。",
            ["empty.noMatch"] = "没有符合条件的条目。",
            ["shaZhao.lifecycle"] = "生命周期：{Lifecycle}",
            ["shaZhao.lifecycle.instant"] = "瞬发",
            ["shaZhao.lifecycle.charged"] = "次数型",
            ["shaZhao.lifecycle.staged"] = "阶段型",
            ["shaZhao.lifecycle.sealed"] = "封印型",
        };

    /// <summary>
    /// 用本模组的蛊方大全表本地化文案。表名与键前缀固定，
    /// 调用处只给 <c>GU_ZHEN_REN_RUBILD_COMPENDIUM.</c> 之后的键段。
    /// </summary>
    private static LocString GetLoc(string keySuffix) =>
        new(LocTableName, LocKeyPrefix + "." + keySuffix);

    private static string T(string keySuffix) => T(keySuffix, []);

    private static string T(
        string keySuffix,
        params (string Name, object Value)[] variables
    )
    {
        if (UseBuiltInText)
        {
            return FormatBuiltIn(keySuffix, variables);
        }

        try
        {
            LocString locString = GetLoc(keySuffix);

            foreach ((string name, object value) in variables)
            {
                // AddObj 而非 Add：Add 的重载只接受 decimal/bool/string，
                // 这里要同时传整数与字符串。
                locString.AddObj(name, value);
            }

            return locString.GetFormattedText();
        }
        catch (Exception exception)
        {
            Warn(
                $"蛊方大全文案缺失（{LocKeyPrefix}.{keySuffix}），已退回内置中文：" +
                exception.Message
            );
            return FormatBuiltIn(keySuffix, variables);
        }
    }

    /// <summary>
    /// 用内置中文替换 <c>{Name}</c> 占位符。
    ///
    /// 只替换代码自己传入的参数；形如 <c>[ShaZhaoRecipe]</c> 的方括号文本
    /// 不是占位符，保持原样输出。
    /// </summary>
    private static string FormatBuiltIn(
        string keySuffix,
        params (string Name, object Value)[] variables
    )
    {
        if (!BuiltInChineseText.TryGetValue(keySuffix, out string? text))
        {
            return LocKeyPrefix + "." + keySuffix;
        }

        foreach ((string name, object value) in variables)
        {
            text = text.Replace(
                "{" + name + "}",
                Convert.ToString(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture
                ) ?? string.Empty,
                StringComparison.Ordinal
            );
        }

        return text;
    }

    /// <summary>
    /// 统一的警告出口。走 <c>GD.PushWarning</c> 而不是 <c>Entry.Logger</c>：
    /// 本类是 <c>[Tool]</c> 脚本，编辑器进程里 <c>Entry</c> 未必初始化过，
    /// 而 PushWarning 在编辑器与游戏里都可用（游戏同样会落进 godot.log）。
    /// </summary>
    private static void Warn(string message) => GD.PushWarning(message);

    /// <summary>
    /// 转数的中文写法。与卡面头部的 <c>{RankCN}</c> 共用同一个工具，
    /// 避免界面与卡面各写一套数字表。
    /// </summary>
    private static string ToChineseRank(int rank) =>
        ChineseNumber.ToChineseNumber(rank);

    private enum RecipeCategory
    {
        Gu,
        Card,
        HeLian,
        ShaZhao,
    }

    private sealed record RecipeViewModel(
        RecipeCategory Category,
        string ResultName,
        Type ResultType,
        IReadOnlyList<Type> MaterialTypes,
        int MinimumMaterialRank,
        string SearchText
    );
}
