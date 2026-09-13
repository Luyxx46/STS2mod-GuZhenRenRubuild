using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using System.Diagnostics.CodeAnalysis;

namespace GuZhenRenRubild.Cards.Core.Catalog;

/// <summary>
/// 蛊方大全的只读视图模型。
///
/// 这一层只负责"把注册表与卡池翻成界面能直接画的数据"，不做任何本地化：
/// 稀有度、卡牌类型、费用等一律返回原始值（枚举 / int），
/// 文案由界面层的本地化助手拼。这样文案改动不会牵动数据层。
/// </summary>
internal sealed record GuDetail(
    Type CardType,
    string Name,
    int GuRank,
    CardRarity Rarity,
    CardType Kind,
    int YuanQiCost,
    int MaxUses,
    int RecoveryDelayTurns,
    string Description,
    string? DetailDescription,
    IReadOnlyList<GuAcquisitionKind> Acquisitions,
    int StarterCopyCount,
    IReadOnlyList<string> ExtraAcquisitionKeys,
    string? CompanionName,
    Type? CompanionType,
    IReadOnlyList<GuRecipeLink> ProducedBy,
    IReadOnlyList<GuRecipeLink> UsedAsHeLianMaterial,
    IReadOnlyList<GuRecipeLink> UsedAsShaZhaoMaterial);

/// <summary>
/// 一条与某张卡相关的配方。
///
/// 两个方向的查询共用它：
/// 1. "产出"——<see cref="ResultType"/> 是这张卡本身；
/// 2. "作为材料"——<see cref="MaterialTypes"/> 里含这张卡。
/// </summary>
internal sealed record GuRecipeLink(
    Type ResultType,
    string ResultName,
    IReadOnlyList<Type> MaterialTypes,
    int MinimumMaterialRank,
    int CostYuanQi,
    int CostEnergy,
    bool IsShaZhao);

/// <summary>
/// 一张普通牌的只读视图模型（普通牌、伴生牌、杀招推演系统牌等）。
///
/// 普通牌没有转数、获取方式与合练/杀招路径，因此只保留与蛊牌共有的
/// 展示字段：贴图、名称、费用、稀有度、类型与描述。
/// 蛊牌专属字段由 <see cref="GuDetail"/> 承载，两者由
/// <see cref="GuCompendiumModel.TryBuildCardView"/> 区分返回。
/// </summary>
internal sealed record PlainCardDetail(
    Type CardType,
    string Name,
    int EnergyCost,
    CardRarity Rarity,
    CardType Kind,
    string Description,
    string? DetailDescription,
    bool GainsBlock);

/// <summary>
/// 蛊方大全的数据组装器。
///
/// 反向索引（"哪些配方用到了这张卡"）在每次组装时线性扫一遍注册表。
/// 配方数量是几十条量级，比维护一份缓存更简单，也不会因为注册表延迟构建而失效。
/// </summary>
internal static class GuCompendiumModel
{
    /// <summary>蛊虫大全的名单：蛊牌主奖励池里的全部蛊牌（含合练结果蛊）。</summary>
    internal static IReadOnlyList<CardModel> GetAllGuCards() =>
        GuCardCatalog.AllCards
            .Where(static card => card is IGuCard)
            .ToArray();

    /// <summary>
    /// 卡牌大全的名单：本模组全部卡池（蛊牌池 + 杀招池 + 辅助池）里的每一张牌。
    ///
    /// 伴生牌、杀招推演这类系统牌与未来的杀招牌都不在蛊牌池里，
    /// 只有走 <see cref="GuCardCatalog.AllModCards"/> 才能把它们一并收录。
    /// </summary>
    internal static IReadOnlyList<CardModel> GetAllCards() =>
        GuCardCatalog.AllModCards.ToArray();

    /// <summary>杀招名单：杀招专属卡池里的全部杀招。</summary>
    internal static IReadOnlyList<CardModel> GetAllShaZhaoCards() =>
        ShaZhaoCardCatalog.AllCards
            .Where(static card => card is AbstractShaZhaoCard)
            .ToArray();

    /// <summary>
    /// 组装一张卡的查看页数据：蛊牌与杀招返回 <see cref="GuDetail"/>，
    /// 其余（普通牌 / 伴生牌 / 系统牌）返回 <see cref="PlainCardDetail"/>。
    /// 认不出类型时返回 null，由调用方决定提示方式，不抛异常打断界面。
    /// </summary>
    internal static object? TryBuildCardView(Type cardType, int previewRank)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        if (!TryCreatePreview(cardType, previewRank, out CardModel? preview))
        {
            return null;
        }

        return preview switch
        {
            AbstractGuCard guCard => BuildGuDetail(guCard, cardType),
            AbstractShaZhaoCard shaZhao => BuildShaZhaoDetail(shaZhao, cardType),
            _ => BuildPlainCardDetail(preview, cardType),
        };
    }

    /// <summary>组装一只蛊的详情；调用方只关心蛊牌时用它。</summary>
    internal static GuDetail? BuildDetail(Type cardType, int previewRank) =>
        TryBuildCardView(cardType, previewRank) as GuDetail;

    /// <summary>组装一张普通牌的查看页数据。</summary>
    private static PlainCardDetail BuildPlainCardDetail(
        CardModel card,
        Type cardType
    ) => new(
        cardType,
        card.Title,
        card.EnergyCost.Canonical,
        card.Rarity,
        card.Type,
        card.GetDescriptionForPile(PileType.None),
        TryGetDetailDescription(card),
        card.GainsBlock
    );

    /// <summary>
    /// 生成一张只读预览副本。蛊牌会写入预览转数（纯显示，不写存档附加状态），
    /// 杀招与普通卡不改转数。
    /// </summary>
    private static bool TryCreatePreview(
        Type cardType,
        int previewRank,
        [NotNullWhen(true)] out CardModel? preview
    )
    {
        preview = null;

        // 先在两池里找规范实例。注意不能用 `!A(...) && !B(...)` 的短路写法：
        // 那样编译器无法确定 out 参数最终非空。
        if (!GuCardCatalog.TryFindCanonical(cardType, out CardModel? canonical))
        {
            ShaZhaoCardCatalog.TryFindCanonical(cardType, out canonical);
        }

        if (canonical == null)
        {
            Entry.Logger.Warn(
                $"蛊方大全：找不到卡牌类型 {cardType.FullName}，已跳过详情。"
            );
            return false;
        }

        CardModel mutable = canonical.ToMutable();

        if (mutable is AbstractGuCard guCard)
        {
            // persist: false —— 预览不写 SavedAttachedState，避免给一次性副本留持久化条目。
            guCard.InitializeGuRankForPreview(
                Math.Clamp(
                    previewRank,
                    AbstractGuCard.MinimumGuRank,
                    guCard.MaxGuRank
                )
            );
        }

        preview = mutable;
        return true;
    }

    private static GuDetail BuildGuDetail(
        AbstractGuCard card,
        Type cardType
    )
    {
        IGuCard gu = card;
        string? companionName = null;
        Type? companionType = null;

        if (card is ICompanionSourceGuCard companionSource)
        {
            companionType = companionSource.Companion.CardType;
            companionName = TryGetCardName(companionType);
        }

        return new GuDetail(
            cardType,
            card.Title,
            gu.GuRank,
            card.Rarity,
            card.Type,
            gu.YuanQiCost,
            gu.MaxUses,
            gu.RecoveryDelayTurns,
            card.GetDescriptionForPile(PileType.None),
            TryGetDetailDescription(card),
            GuAcquisitionResolver.Resolve(card),
            GuAcquisitionResolver.GetStarterCopyCount(cardType),
            GuAcquisitionOverrides.GetExtraAcquisitionKeys(cardType),
            companionName,
            companionType,
            FindRecipesProducing(cardType),
            FindRecipesUsingAsMaterial(cardType, isShaZhao: false),
            FindRecipesUsingAsMaterial(cardType, isShaZhao: true)
        );
    }

    private static GuDetail BuildShaZhaoDetail(
        AbstractShaZhaoCard card,
        Type cardType
    )
    {
        return new GuDetail(
            cardType,
            card.Title,
            card.GuRank,
            card.Rarity,
            card.Type,
            // 杀招不走元气、固定 0 转起步；费用用原生能量表示。
            YuanQiCost: 0,
            MaxUses: Math.Max(1, card.ShaZhaoMaxUses),
            // 杀招没有冷却概念，0 表示界面不显示这一项。
            RecoveryDelayTurns: 0,
            card.GetDescriptionForPile(PileType.None),
            TryGetDetailDescription(card),
            Acquisitions: [],
            StarterCopyCount: 0,
            ExtraAcquisitionKeys: [],
            CompanionName: null,
            CompanionType: null,
            ProducedBy: FindRecipesProducing(cardType),
            UsedAsHeLianMaterial: FindRecipesUsingAsMaterial(
                cardType,
                isShaZhao: false
            ),
            UsedAsShaZhaoMaterial: FindRecipesUsingAsMaterial(
                cardType,
                isShaZhao: true
            )
        );
    }

    /// <summary>找出所有以该类型为结果的合练与杀招配方。</summary>
    private static IReadOnlyList<GuRecipeLink> FindRecipesProducing(
        Type cardType
    )
    {
        List<GuRecipeLink> links = [];

        foreach ((Type resultType, IReadOnlyList<Type> materials, int minimumRank)
                 in HeLianRecipeRegistry.GetRecipeDetails())
        {
            if (resultType == cardType)
            {
                links.Add(BuildLink(
                    resultType,
                    materials,
                    minimumRank,
                    isShaZhao: false
                ));
            }
        }

        foreach ((Type resultType, IReadOnlyList<Type> materials, int minimumRank)
                 in ShaZhaoRecipeRegistry.GetRecipeDetails())
        {
            if (resultType == cardType)
            {
                links.Add(BuildLink(
                    resultType,
                    materials,
                    minimumRank,
                    isShaZhao: true
                ));
            }
        }

        return links;
    }

    /// <summary>找出所有把该类型当材料的合练与杀招配方。</summary>
    private static IReadOnlyList<GuRecipeLink> FindRecipesUsingAsMaterial(
        Type cardType,
        bool isShaZhao
    )
    {
        IEnumerable<(
            Type ResultCardType,
            IReadOnlyList<Type> MaterialCardTypes,
            int MinimumMaterialRank
        )> recipes = isShaZhao
            ? ShaZhaoRecipeRegistry.GetRecipeDetails()
            : HeLianRecipeRegistry.GetRecipeDetails();

        List<GuRecipeLink> links = [];

        foreach ((Type resultType, IReadOnlyList<Type> materials, int minimumRank)
                 in recipes)
        {
            if (!materials.Contains(cardType))
            {
                continue;
            }

            links.Add(BuildLink(
                resultType,
                materials,
                minimumRank,
                isShaZhao
            ));
        }

        return links;
    }

    private static GuRecipeLink BuildLink(
        Type resultType,
        IReadOnlyList<Type> materialTypes,
        int minimumMaterialRank,
        bool isShaZhao
    )
    {
        // 费用按结果牌读取：合练结果蛊走元气，杀招走原生能量。
        int yuanQi = 0;
        int energy = 0;

        if (TryCreatePreview(
                resultType,
                AbstractGuCard.MinimumGuRank,
                out CardModel? resultPreview
            ))
        {
            if (resultPreview is IGuCard gu)
            {
                yuanQi = gu.YuanQiCost;
            }
            else
            {
                energy = resultPreview.EnergyCost.Canonical;
            }
        }

        return new GuRecipeLink(
            resultType,
            TryGetCardName(resultType) ?? resultType.Name,
            materialTypes,
            minimumMaterialRank,
            yuanQi,
            energy,
            isShaZhao
        );
    }

    /// <summary>按类型取卡牌标题；取不到时返回 null。</summary>
    internal static string? TryGetCardName(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        if (!GuCardCatalog.TryFindCanonical(cardType, out CardModel? canonical))
        {
            ShaZhaoCardCatalog.TryFindCanonical(cardType, out canonical);
        }

        return canonical?.Title;
    }

    // 详细说明与卡面描述同住在 cards 表、同一张卡的键下，只差一个后缀：
    // 卡面 <CARD_KEY>.description，详细说明 <CARD_KEY>.detailDescription。
    // 键由卡牌自身的 Id.Entry 推导，因此新增卡牌只要补上文案就会被蛊方大全收录。
    private const string CardLocTableName = "cards";
    private const string DetailDescriptionSuffix = ".detailDescription";

    /// <summary>
    /// 取一张卡的"详细说明"（蛊方大全专用的最详细文案）；没有配置时返回 null，
    /// 由界面隐藏该分区而不是显示半截文本。
    ///
    /// 详细说明**不注入任何卡面占位符**（{Rank}/{Damage:diff()} 之类只注入卡面），
    /// 因此文案里的数值一律用文字写公式，避免出现无法解析的占位符。
    /// </summary>
    internal static string? TryGetDetailDescription(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        string key = card.Id.Entry + DetailDescriptionSuffix;

        try
        {
            string? text = LocString.GetIfExists(
                CardLocTableName,
                key
            )?.GetFormattedText();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception exception)
        {
            // 编辑器预览进程里没有 LocManager，这里不抛异常打断界面。
            Entry.Logger.Warn(
                $"蛊方大全：读取 {key} 详细说明失败：{exception.Message}"
            );
            return null;
        }
    }
}
