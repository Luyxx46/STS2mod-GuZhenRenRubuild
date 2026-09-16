using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 蛊强化的「每场战斗可用次数」账本 —— 强化的触发次数不记在附魔实例上，而是记在这里。
///
/// <para>
/// 为什么不用实例字段：需求是「每场战斗 N 次」。若把剩余次数记在附魔实例上，
/// 玩家在次数耗尽、强化槽被清空后<b>再次催动父蛊</b>就会得到一份全新的满次数强化，
/// 同一场战斗里可以反复刷次数。账本以<b>子卡的战斗卡实例</b>为键、按强化类型分账，
/// 因此同一场战斗内重复授予只会保留既有剩余次数，只有进入下一场战斗
/// （战斗卡实例整体换新）才会重新获得完整次数。
/// </para>
///
/// <para>
/// 账本是<b>纯运行时状态</b>：键为 <see cref="CardModel"/> 实例的弱引用，随战斗卡一起回收；
/// 不写进存档。已知取舍：战斗中途存读档会重建战斗卡，账本随之清空、次数回满。
/// </para>
/// </summary>
internal static class CompanionEnhancementUses
{
    // 键用战斗卡实例：每场战斗的卡都是新对象，账本因此天然按场重置。
    private static readonly ConditionalWeakTable<CardModel, Dictionary<Type, int>> Ledger = new();

    /// <summary>读取剩余次数；该（卡，强化类型）尚未记账时返回 null。</summary>
    internal static int? Peek(CardModel card, Type enhancementType)
    {
        if (!Ledger.TryGetValue(card, out Dictionary<Type, int>? uses))
        {
            return null;
        }

        return uses.TryGetValue(enhancementType, out int remaining) ? remaining : null;
    }

    /// <summary>
    /// 授予时记账：首次授予写入完整次数；已有记录则保留当前剩余次数（重复授予不回满）。
    /// </summary>
    internal static void Seed(CardModel card, Type enhancementType, int total)
    {
        Dictionary<Type, int> uses = Ledger.GetOrCreateValue(card);
        if (!uses.ContainsKey(enhancementType))
        {
            uses[enhancementType] = total;
        }
    }

    /// <summary>消耗一次，返回消耗后的剩余次数。</summary>
    internal static int Consume(CardModel card, Type enhancementType)
    {
        Dictionary<Type, int> uses = Ledger.GetOrCreateValue(card);
        int remaining = uses.TryGetValue(enhancementType, out int current) ? current : 0;
        if (remaining > 0)
        {
            remaining--;
        }

        uses[enhancementType] = remaining;
        return remaining;
    }
}
