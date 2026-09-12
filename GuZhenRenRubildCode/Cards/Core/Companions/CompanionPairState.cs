using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>
/// 永久配对身份。网络 ID 仅用于运行时映射，绝不写入存档。
/// </summary>
internal static class CompanionPairState
{
    internal static readonly SavedAttachedState<CardModel, int> PairIdState =
        new(Entry.ModId + ".companion_pair_id", static () => 0);

    internal static int Get(CardModel card) => PairIdState[card];

    internal static void Set(CardModel card, int pairId) =>
        PairIdState[card] = Math.Max(0, pairId);
}
