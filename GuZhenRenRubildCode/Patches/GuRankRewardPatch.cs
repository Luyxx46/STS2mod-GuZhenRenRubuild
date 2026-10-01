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

        foreach (CardModel card in __instance.Cards)
        {
            if (card is not AbstractGuCard gu ||
                !gu.NeedsInitialRankAssignment)
            {
                continue;
            }

            AssignInitialRank(gu, player, RngStream);
        }
    }

    /// <summary>
    /// 为一张尚未获得初始品阶的蛊牌执行一次确定性随机赋阶，并登记仙蛊楼层。
    ///
    /// 供两条入口复用：卡牌奖励（<c>CardReward.Populate</c>）与原版变形结果
    /// （<see cref="GuTransformRankPatch"/>，裁决 D-04）。
    /// <paramref name="rngStream"/> 区分调用来源，保证各入口的随机流互不干扰；
    /// 每张真正需要初始化的蛊牌只推进对应随机流一次（已初始化的卡不消耗随机数，
    /// 界面重建/重复调用仍保持确定性）。
    /// </summary>
    internal static void AssignInitialRank(
        AbstractGuCard gu,
        Player player,
        string rngStream
    )
    {
        ArgumentNullException.ThrowIfNull(gu);
        ArgumentNullException.ThrowIfNull(player);

        // 仙蛊唯一性封顶：整局中已有同名仙蛊时，本次赋阶上限压到五转，
        // 使该入口永远不会直接产出第二张同名仙蛊（拿到手后再升转也会被升炼仲裁拦下）。
        int maximumRank = GuXianGuRules.HasSameXianGu(
            player.RunState,
            gu
        )
            ? Math.Min(
                gu.MaxGuRank,
                GuXianGuRules.XianGuRank - 1
            )
            : gu.MaxGuRank;

        if (!gu.TryAssignInitialRank(
                new Rng(
                    RitsuLibFramework
                        .GetModPlayerRng(player, Entry.ModId, rngStream)
                        .NextUnsignedInt()
                ),
                player.RunState.TotalFloor,
                maximumRank
            ))
        {
            return;
        }

        // 六转及以上的牌一诞生就是仙蛊，必须立刻登记首次成仙楼层；
        // 否则它会被当成"未登记的旧档仙蛊"，反过来把更早的合法仙蛊压掉。
        GuXianGuRules.RegisterXianGuClaim(
            gu,
            player.RunState.TotalFloor
        );
    }
}
