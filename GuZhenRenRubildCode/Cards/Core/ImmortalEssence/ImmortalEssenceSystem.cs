using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards.Core.ImmortalEssence;

/// <summary>
/// 仙元：六转（仙窍）之后使用的第二种"货币"。
///
/// 它**不是**副资源，而是一张停在蛊手牌堆里的货币牌（见
/// <see cref="Abstractions.AbstractXianYuanCard"/>）：牌上带着若干个"催动单位"余额，
/// 六转及以上的蛊牌每次催动在照付元气之外**额外**消耗单位，
/// 玩家也可以主动打出这张牌，花掉 1 个单位把元气补满。
///
/// 余额写在卡牌自身的 <see cref="SavedAttachedState{TModel,TValue}"/> 上，
/// 因此随着战斗快照与多人同步一起恢复，不需要额外的存档字段；
/// 牌是"每场战斗新生成一张"，所以余额会随着新牌自然回满。
/// </summary>
internal static class ImmortalEssenceSystem
{
    // 从未扣减过的牌用 -1 表示"取满额"，这样刚生成的牌不需要任何初始化写入。
    private const int UninitializedUnits = -1;

    private static readonly SavedAttachedState<CardModel, int> RemainingUnitsState =
        new(Entry.ModId + ".immortal_essence_units", static () => UninitializedUnits);

    /// <summary>
    /// 六转及以上蛊牌每次催动额外消耗的仙元单位：
    /// 六 / 七 / 八 / 九转分别为 1 / 2 / 4 / 8（六转以下为 0，即只花元气）。
    /// </summary>
    internal static int GetActivationCost(int guRank)
    {
        if (guRank < ApertureProgression.ImmortalRank)
        {
            return 0;
        }

        int exponent = Math.Clamp(
            guRank - ApertureProgression.ImmortalRank,
            0,
            3
        );
        return 1 << exponent;
    }

    /// <summary>这张仙元牌一共提供多少单位；非仙元牌返回 0。</summary>
    internal static int GetTotalUnits(CardModel card) =>
        card is AbstractXianYuanCard essence ? essence.ActivationUnits : 0;

    /// <summary>这张仙元牌还剩多少单位；非仙元牌返回 0。</summary>
    internal static int GetRemainingUnits(CardModel card)
    {
        if (card is not AbstractXianYuanCard essence)
        {
            return 0;
        }

        int saved = RemainingUnitsState[card];
        return saved < 0
            ? essence.ActivationUnits
            : Math.Clamp(saved, 0, essence.ActivationUnits);
    }

    /// <summary>
    /// 当前玩家手头可用的仙元总量。
    ///
    /// 这是可打出判定与气泡提醒都会高频调用的路径（蛊手牌界面每次刷新都会问），
    /// 因此这里刻意不排序列、不建临时集合，只做一次遍历求和。
    /// </summary>
    internal static int GetAvailableUnits(Player? owner)
    {
        if (owner == null)
        {
            return 0;
        }

        int total = 0;
        foreach (CardModel card in GuCardPileSystem.ActivePileType.GetPile(owner).Cards)
        {
            total += GetRemainingUnits(card);
        }

        return total;
    }

    /// <summary>
    /// 六转及以上的蛊牌是否付得起仙元。六转以下恒为真（只花元气）。
    /// 只读状态，不产生副作用，供 <see cref="GuCardRuntime.CanActivate"/> 调用。
    /// </summary>
    internal static bool CanPayActivation(CardModel guCard)
    {
        if (guCard is not IGuCard gu)
        {
            return true;
        }

        int cost = GetActivationCost(gu.GuRank);
        return cost <= 0 || GetAvailableUnits(guCard.Owner) >= cost;
    }

    /// <summary>
    /// 按"先花小面额"的顺序扣减仙元单位，避免小额需求把大面额牌打散。
    ///
    /// 与旧模组一致的确定性排序：余额升序 → <c>Id.Entry</c>。
    /// 本仓库同一场战斗里只会发放一张仙元牌，排序是为后续可能出现多张牌预留的。
    /// </summary>
    internal static bool TrySpend(Player owner, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (GetAvailableUnits(owner) < amount)
        {
            return false;
        }

        AbstractXianYuanCard[] essences = GuCardPileSystem.ActivePileType
            .GetPile(owner)
            .Cards
            .OfType<AbstractXianYuanCard>()
            .OrderBy(GetRemainingUnits)
            .ThenBy(static card => card.Id.Entry, StringComparer.Ordinal)
            .ToArray();

        int remaining = amount;
        foreach (AbstractXianYuanCard essence in essences)
        {
            if (remaining <= 0)
            {
                break;
            }

            int available = GetRemainingUnits(essence);
            if (available <= 0)
            {
                continue;
            }

            int spent = Math.Min(available, remaining);
            RemainingUnitsState[essence] = available - spent;
            remaining -= spent;
        }

        return remaining <= 0;
    }

    /// <summary>
    /// 主动打出仙元牌：消耗 1 个单位，并把元气补满到当前上限。
    /// 余额不足时不做任何事（可打出判定本应已经拦住，这里只留一条日志）。
    /// </summary>
    internal static async Task PlayEssenceAsync(
        AbstractXianYuanCard essence,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (essence.Owner is not { } owner)
        {
            return;
        }

        if (!TrySpend(owner, 1))
        {
            Entry.Logger.Warn(
                $"[仙元] {essence.Id} 余额不足，主动使用被跳过。"
            );
            return;
        }

        await RefillYuanQiAsync(owner, essence);
    }

    /// <summary>把元气直接补到当前上限（上限由空窍遗物按转数改写）。</summary>
    internal static async Task RefillYuanQiAsync(Player owner, AbstractModel source)
    {
        if (owner.PlayerCombatState == null)
        {
            return;
        }

        int maximum = SecondaryResourceCmd.GetMax(owner, YuanQiSystem.ResourceId)
            ?? YuanQiSystem.Definition.HardMaxAmount;
        int current = SecondaryResourceCmd.Get(owner, YuanQiSystem.ResourceId);

        if (current >= maximum)
        {
            return;
        }

        await SecondaryResourceCmd.Set(
            owner,
            YuanQiSystem.ResourceId,
            maximum,
            source: source
        );
    }
}
