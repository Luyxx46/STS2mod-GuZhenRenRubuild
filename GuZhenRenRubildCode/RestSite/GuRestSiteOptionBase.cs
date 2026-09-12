using Godot;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.RestSite;

/// <summary>
/// 本模组篝火选项的公共父类。
///
/// 两个选项的选项编号、描述文本表、图标回退、资源预加载、本地/远端动效
/// 以及结果弹窗完全一致，只有"可选条件"和"选择流程"不同，因此这里把
/// 公共部分固定下来，子类只需实现 <see cref="OnSelect"/>、
/// <see cref="IsEnabled"/> 与 <see cref="LocalVfxCards"/>。
///
/// 结果弹窗的标题与正文都从 <c>rest_site_ui</c> 本地化表读取，
/// 键名规则为 <c>OPTION_{OptionId}.{keySuffix}</c>，不在代码里写死任何语言文本。
/// </summary>
public abstract class GuRestSiteOptionBase : ModRestSiteOptionTemplate
{
    /// <summary>选项图标缺失时统一回退到原版锻造图标。</summary>
    public const string FallbackIconPath =
        "res://images/ui/rest_site/option_smith.png";

    private const string LocTable = "rest_site_ui";

    // 篝火选项的本地化键统一带 OPTION_ 前缀；游戏也是按 OPTION_{OptionId}.name 取名字的，
    // 因此 OptionId 本身保持不带前缀，只有键名由这里拼装。
    private const string OptionKeyPrefix = "OPTION_";

    private readonly string _optionId;
    private readonly string _iconPath;
    private readonly LocString _description;

    protected GuRestSiteOptionBase(
        Player owner,
        string optionId,
        string? iconPath = null
    ) : base(owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);

        _optionId = optionId;
        _iconPath = string.IsNullOrWhiteSpace(iconPath)
            ? FallbackIconPath
            : iconPath;
        _description = new LocString(
            LocTable,
            BuildLocKey(optionId, "description")
        );
    }

    public sealed override string OptionId => _optionId;

    public sealed override LocString Description => _description;

    // 模组图标尚未入库时回退到原版锻造图标，保证按钮预加载与渲染正常。
    public override RestSiteOptionAssetProfile AssetProfile => new(
        IconPath: ResourceLoader.Exists(_iconPath)
            ? _iconPath
            : FallbackIconPath
    );

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(NCardSmithVfx.AssetPaths);

    /// <summary>
    /// 本地端动效需要展示的卡牌，由子类在 <see cref="OnSelect"/> 中填充。
    /// 返回空集合表示本次没有需要播放动效的结果。
    /// </summary>
    protected abstract IReadOnlyList<CardModel> LocalVfxCards { get; }

    public sealed override async Task DoLocalPostSelectVfx(
        CancellationToken ct = default
    )
    {
        IReadOnlyList<CardModel> cards = LocalVfxCards;

        if (cards.Count == 0)
        {
            return;
        }

        NCardSmithVfx? vfx = NCardSmithVfx.Create(cards);

        if (vfx == null)
        {
            return;
        }

        NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);

        await Cmd.CustomScaledWait(1f, 2f, ignoreCombatEnd: false, ct);
    }

    public sealed override Task DoRemotePostSelectVfx()
    {
        NRestSiteCharacter? characterNode = NRestSiteRoom.Instance?
            .Characters
            .FirstOrDefault(character => character.Player == Owner);

        NCardSmithVfx? vfx = NCardSmithVfx.Create();

        if (characterNode == null || vfx == null)
        {
            return Task.CompletedTask;
        }

        characterNode.AddChildSafely(vfx);
        vfx.Position = Vector2.Zero;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 取出本选项在 <c>rest_site_ui</c> 表中的文本，
    /// 键名为 <c>OPTION_{OptionId}.{keySuffix}</c>。
    /// </summary>
    protected LocString GetOptionText(string keySuffix) =>
        new(LocTable, BuildLocKey(_optionId, keySuffix));

    private static string BuildLocKey(string optionId, string keySuffix) =>
        $"{OptionKeyPrefix}{optionId}.{keySuffix}";

    /// <summary>
    /// 向本地玩家显示一次结果弹窗。标题由 <paramref name="success"/>
    /// 决定，正文由 <paramref name="bodyKeySuffix"/> 指定并支持命名参数。
    /// 该提示不参与游戏状态与多人同步。
    /// </summary>
    protected void ShowLocalFeedback(
        bool success,
        string bodyKeySuffix,
        params (string Name, string Value)[] arguments
    )
    {
        if (!LocalContext.IsMe(Owner))
        {
            return;
        }

        NModalContainer? modalContainer = NModalContainer.Instance;

        if (modalContainer == null || modalContainer.OpenModal != null)
        {
            Entry.Logger.Info(
                "无法显示篝火选项结果弹窗：当前没有可用模态容器，" +
                "或已有其他弹窗打开。"
            );
            return;
        }

        string title = FormatOptionText(
            success ? "feedback.successTitle" : "feedback.failureTitle"
        );
        string body = FormatOptionText(bodyKeySuffix, arguments);

        NErrorPopup? popup = NErrorPopup.Create(
            title,
            body,
            showReportBugButton: false
        );

        if (popup != null)
        {
            modalContainer.Add(popup);
        }
    }

    /// <summary>
    /// 取本地化原文并按命名参数替换占位符。
    ///
    /// 弹窗正文既包含数字（结果转数）也包含字符串（材料名、异常信息），
    /// 而 <see cref="LocString"/> 的数值参数入口只接受数字，
    /// 因此这里统一按 <c>{Name}</c> 占位符替换，文案本身仍全部留在
    /// 本地化表里，代码不感知任何语言。
    /// </summary>
    private string FormatOptionText(
        string keySuffix,
        params (string Name, string Value)[] arguments
    )
    {
        string text = GetOptionText(keySuffix).GetRawText();

        foreach ((string name, string value) in arguments)
        {
            text = text.Replace(
                "{" + name + "}",
                value,
                StringComparison.Ordinal
            );
        }

        return text;
    }
}
