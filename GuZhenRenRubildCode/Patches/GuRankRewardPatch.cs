using System.Reflection;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
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

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    // 在 CardReward.Populate 完成后处理奖励列表，确保原生奖励卡已经全部生成。
    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodBase populate = RequiredMember.Method(
                typeof(CardReward),
                nameof(CardReward.Populate)
            );

            harmony.Patch(
                populate,
                postfix: new HarmonyMethod(
                    typeof(GuRankRewardPatch),
                    nameof(PopulatePostfix)
                )
            );
        });
    }

    // 移除本补丁组，并恢复可再次初始化的状态。
    internal static void Uninitialize()
    {
        Host.Unpatch();
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

            // 仙蛊唯一性封顶：整局中已有同名仙蛊时，本次赋阶上限压到五转，
            // 使奖励永远不会直接产出第二张同名仙蛊（拿到手后再升转也会被升炼仲裁拦下）。
            int maximumRank = GuXianGuRules.HasSameXianGu(
                player.RunState,
                card
            )
                ? Math.Min(
                    gu.MaxGuRank,
                    GuXianGuRules.XianGuRank - 1
                )
                : gu.MaxGuRank;

            // 每张真正需要初始化的蛊牌只推进主随机流一次，再用得到的种子建立独立随机器。
            // 这样奖励界面重建或 Populate 被重复调用时，已经初始化的卡不会再次消耗随机数，结果仍保持确定性。
            // 封顶只改变采样区间，不额外消耗随机数，随机流的推进次数保持不变。
            gu.TryAssignInitialRank(
                new Rng(stream.NextUnsignedInt()),
                player.RunState.TotalFloor,
                maximumRank
            );

            // 六转及以上的奖励牌一诞生就是仙蛊，必须立刻登记首次成仙楼层；
            // 否则它会被当成"未登记的旧档仙蛊"，反过来把更早的合法仙蛊压掉。
            GuXianGuRules.RegisterXianGuClaim(
                gu,
                player.RunState.TotalFloor
            );
        }
    }
}
