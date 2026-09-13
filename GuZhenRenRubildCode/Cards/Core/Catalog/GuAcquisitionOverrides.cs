namespace GuZhenRenRubild.Cards.Core.Catalog;

/// <summary>
/// 获取方式的人工兜底表。
///
/// 绝大多数获取方式都能从代码推导（见 <see cref="GuAcquisitionResolver"/>）：
/// 初始牌组读 <c>[RegisterCharacterStarterCard]</c>、卡牌奖励读
/// <c>GuCardRewardRules.CanAppear</c>、合练与杀招路径读两个配方注册表、
/// 伴生牌读 <c>ICompanionSourceGuCard</c>。
///
/// 但有两类东西推不出来，必须在这里声明：
/// 1. 初始张数——RitsuLib 特性的属性名不在 XML 文档里，反射读属性有不确定性，
///    一旦读不到就靠这张表补；
/// 2. 以后出现的、不由代码结构决定的渠道（事件获取、遗物给牌、商店等）。
///
/// 新增蛊牌时如果只用标准渠道，**不需要动这个文件**。
/// </summary>
internal static class GuAcquisitionOverrides
{
    /// <summary>
    /// 某只蛊的人工补充获取方式。键是卡牌类型，值是本地化键段
    /// （相对 <c>GU_ZHEN_REN_RUBILD_COMPENDIUM.acquisition.</c> 的键段）。
    /// </summary>
    private static readonly Dictionary<Type, string[]> ExtraAcquisitionKeys =
        new()
        {
            // 目前所有蛊的获取方式都能推导，此处留空。
        };

    /// <summary>
    /// 初始牌组张数的人工覆盖。仅当无法从特性反射出张数时才会被采用；
    /// 值必须与卡牌上的 <c>[RegisterCharacterStarterCard(..., count)]</c> 一致。
    /// </summary>
    private static readonly Dictionary<Type, int> StarterCopyCounts =
        new()
        {
            // 兜底值：与 [RegisterCharacterStarterCard] 声明的张数保持一致。
            [typeof(global::GuZhenRenRubild.Cards.Basic.YuPiGu.YuPiGu)] = 1,
            [typeof(global::GuZhenRenRubild.Cards.Basic.YueGuangGu.YueGuangGu)] = 2,
        };

    /// <summary>取人工补充的获取方式键段；没有时返回空集合。</summary>
    internal static IReadOnlyList<string> GetExtraAcquisitionKeys(
        Type cardType
    )
    {
        ArgumentNullException.ThrowIfNull(cardType);

        return ExtraAcquisitionKeys.TryGetValue(cardType, out string[]? keys)
            ? keys
            : [];
    }

    /// <summary>取初始牌组张数的兜底值；没有声明时返回 0。</summary>
    internal static int GetStarterCopyCount(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        return StarterCopyCounts.TryGetValue(cardType, out int count)
            ? count
            : 0;
    }
}
