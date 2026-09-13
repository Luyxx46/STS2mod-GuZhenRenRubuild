using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Core.Catalog;

/// <summary>一只蛊（或一张杀招）的获取方式类别。</summary>
internal enum GuAcquisitionKind
{
    /// <summary>作为角色初始牌组的一部分发放。</summary>
    StarterDeck,

    /// <summary>可以出现在普通卡牌奖励中。</summary>
    CardReward,

    /// <summary>只能由合练产出（合练专属蛊不进普通奖励）。</summary>
    HeLianOnly,

    /// <summary>由人工兜底表补充的渠道（事件、遗物等）。</summary>
    Manual,
}

/// <summary>
/// 获取方式推导器。
///
/// 推导优先级：
/// 1. 初始牌组：读卡牌类上的 <c>[RegisterCharacterStarterCard]</c>；反射不出张数时
///    退回 <see cref="GuAcquisitionOverrides"/> 的兜底表；
/// 2. 卡牌奖励：<see cref="GuCardRewardRules.CanAppear"/> 对本地玩家的判定；
/// 3. 合练专属：<c>AbstractHeLianGuCard</c> 且未实现 <c>IHeLianCardRewardEligible</c>；
/// 4. 人工补充：<see cref="GuAcquisitionOverrides.GetExtraAcquisitionKeys"/>。
///
/// 只有第 1 步用到反射。RitsuLib 的属性名不在其 XML 文档里，因此属性名按
/// 两个候选逐个尝试，全失败时安静降级到兜底表，绝不让图鉴因此报错。
/// </summary>
internal static class GuAcquisitionResolver
{
    // 特性实例本身不可变，缓存"类型 -> 反射结果"避免每次点击都反射一遍。
    private static readonly Dictionary<Type, int> StarterCounts = [];

    // 本地玩家的奖励判定结果只取决于卡牌类型，同样缓存。
    private static readonly Dictionary<Type, bool> RewardEligibility = [];

    internal static IReadOnlyList<GuAcquisitionKind> Resolve(
        CardModel canonical
    )
    {
        ArgumentNullException.ThrowIfNull(canonical);

        Type cardType = canonical.GetType();
        List<GuAcquisitionKind> kinds = [];

        if (GetStarterCopyCount(cardType) > 0)
        {
            kinds.Add(GuAcquisitionKind.StarterDeck);
        }

        if (CanAppearInReward(canonical))
        {
            kinds.Add(GuAcquisitionKind.CardReward);
        }
        else if (cardType.IsSubclassOf(typeof(AbstractHeLianGuCard)))
        {
            // 合练专属蛊：既不进奖励，也不由初始牌组发放，只能练出来。
            kinds.Add(GuAcquisitionKind.HeLianOnly);
        }

        if (GuAcquisitionOverrides
            .GetExtraAcquisitionKeys(cardType)
            .Count > 0)
        {
            kinds.Add(GuAcquisitionKind.Manual);
        }

        return kinds;
    }

    /// <summary>
    /// 初始牌组张数。先反射特性，失败再用兜底表；两者都没有时返回 0
    /// （表示这只蛊不是初始牌）。
    /// </summary>
    internal static int GetStarterCopyCount(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        if (StarterCounts.TryGetValue(cardType, out int cached))
        {
            return cached;
        }

        int count = ReadStarterCopyCountFromAttribute(cardType);

        if (count <= 0)
        {
            count = GuAcquisitionOverrides.GetStarterCopyCount(cardType);
        }

        StarterCounts[cardType] = count;
        return count;
    }

    /// <summary>
    /// 从 <c>[RegisterCharacterStarterCard(typeof(角色), 张数)]</c> 读张数。
    /// 特性构造函数已核实为 <c>(Type characterType, int count)</c>，
    /// 但承载它的属性名没有文档，因此按多个候选名尝试并整体 try/catch。
    /// </summary>
    private static int ReadStarterCopyCountFromAttribute(Type cardType)
    {
        try
        {
            object[] attributes = cardType.GetCustomAttributes(
                typeof(RegisterCharacterStarterCardAttribute),
                inherit: false
            );

            foreach (object attribute in attributes)
            {
                if (TryReadIntMember(attribute, "Count", out int count) ||
                    TryReadIntMember(attribute, "Amount", out count) ||
                    TryReadIntMember(attribute, "Copies", out count))
                {
                    return Math.Max(0, count);
                }
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"蛊方大全：读取 {cardType.Name} 的初始牌组张数失败，" +
                $"改用兜底表：{exception.Message}"
            );
        }

        return 0;
    }

    private static bool TryReadIntMember(
        object target,
        string memberName,
        out int value
    )
    {
        value = 0;

        try
        {
            Type type = target.GetType();

            System.Reflection.PropertyInfo? property = type.GetProperty(
                memberName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance
            );

            if (property?.GetValue(target) is int fromProperty)
            {
                value = fromProperty;
                return true;
            }

            System.Reflection.FieldInfo? field = type.GetField(
                memberName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance
            );

            if (field?.GetValue(target) is int fromField)
            {
                value = fromField;
                return true;
            }
        }
        catch
        {
            // 属性名不匹配或取不到值：交给下一个候选名/兜底表。
        }

        return false;
    }

    /// <summary>
    /// 该卡牌能否出现在本地玩家的普通卡牌奖励里。
    /// 取不到本地玩家时保守地按"不进奖励"处理，避免图鉴谎报获取渠道。
    /// </summary>
    private static bool CanAppearInReward(CardModel canonical)
    {
        Type cardType = canonical.GetType();

        if (RewardEligibility.TryGetValue(cardType, out bool cached))
        {
            return cached;
        }

        bool eligible = false;

        try
        {
            MegaCrit.Sts2.Core.Entities.Players.Player? player =
                MegaCrit.Sts2.Core.Context.LocalContext.GetMe(
                    MegaCrit.Sts2.Core.Runs.RunManager.Instance.State
                );

            if (player != null)
            {
                eligible = GuCardRewardRules.CanAppear(player, canonical);
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"蛊方大全：判定 {cardType.Name} 是否进奖励失败：{exception.Message}"
            );
        }

        RewardEligibility[cardType] = eligible;
        return eligible;
    }
}
