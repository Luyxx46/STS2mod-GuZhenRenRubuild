using System.Reflection;
using GuZhenRenRubild.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 为卡牌奖励中新出现的蛊牌分配初始品阶。随机流与玩家、模组和固定流名称绑定，因此相同游戏状态下结果可稳定复现。
/// </summary>
internal static class GuRankRewardPatch
{
    // HarmonyId 用于隔离本补丁；RngStream 用于从 RitsuLib 获取专属、可复现的玩家随机流。
    private const string HarmonyId = Entry.ModId + ".GuRankReward";
    private const string RngStream = "reward/gu_rank";
    private static bool _initialized;

    // 在 CardReward.Populate 完成后处理奖励列表，确保原生奖励卡已经全部生成。
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodBase populate = AccessTools.Method(
            typeof(CardReward),
            nameof(CardReward.Populate)
        ) ?? throw new MissingMethodException(
            typeof(CardReward).FullName,
            nameof(CardReward.Populate)
        );

        new Harmony(HarmonyId).Patch(
            populate,
            postfix: new HarmonyMethod(
                typeof(GuRankRewardPatch),
                nameof(PopulatePostfix)
            )
        );
        _initialized = true;
    }

    // 移除本补丁组，并恢复可再次初始化的状态。
    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
    }

    // 遍历本次奖励中的卡牌，只对尚未获得初始品阶的蛊牌执行一次随机抽取。
    private static void PopulatePostfix(CardReward __instance)
    {
        Player player = __instance.Player;
        Rng stream = RitsuLibFramework.GetModPlayerRng(
            player,
            Entry.ModId,
            RngStream
        );

        foreach (CardModel card in __instance.Cards)
        {
            if (card is not AbstractGuCard gu ||
                !gu.NeedsInitialRankAssignment)
            {
                continue;
            }

            // 每张真正需要初始化的蛊牌只推进主随机流一次，再用得到的种子建立独立随机器。
            // 这样奖励界面重建或 Populate 被重复调用时，已经初始化的卡不会再次消耗随机数，结果仍保持确定性。
            gu.TryAssignInitialRank(
                new Rng(stream.NextUnsignedInt()),
                player.RunState.TotalFloor
            );
        }
    }
}
