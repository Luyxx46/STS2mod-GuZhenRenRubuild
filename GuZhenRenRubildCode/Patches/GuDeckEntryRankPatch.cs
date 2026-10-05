using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-10（转数系统，C 式落地）：在 <c>Hook.ShouldAddToDeck</c> 上兜底抽取初始转数。
///
/// <para>
/// 原版把卡牌加入永久牌组的路径统一经过 <c>CardPileCmd.Add</c>（内部对
/// <c>PileType.Deck</c> 调用 <c>Hook.ShouldAddToDeck</c>，全游戏唯一调用点），
/// 覆盖奖励领取、商店购买、事件/遗物给牌、合练结果等一切永久入组；而
/// <see cref="GuRankRewardPatch"/> 只挂在 <c>CardReward.Populate</c> 上，不经卡牌奖励
/// 界面的来源（事件给牌、遗物补牌等）会停在默认一转且不登记仙蛊。本补丁在这些牌
/// 最终放行入组时补抽一次初始转数，堵住转数缺口。
/// </para>
///
/// <para>
/// 与奖励/变形入口共存沿旧版 mod 同款幂等机制：已赋阶的蛊牌
/// （<c>NeedsInitialRankAssignment == false</c>）直接跳过、随机流零消费——卡牌奖励的
/// <c>reward/gu_rank</c> 流推进序列因此与本补丁存在前逐位一致（不漂移），合练结果
/// （材料最高转数 + 1）与变形补赋结果也不会被覆盖。抽样本身完全复用
/// <see cref="GuRankRewardPatch.AssignInitialRank"/>：原版 Rng 正态分布（均值随楼层
/// 1→3、σ=2、±0.5 边界）+ 仙蛊封顶 + 成仙登记，零改动。
/// </para>
///
/// <para>
/// 例外路径：原版变形走 <c>AddInternal</c> 不经过本挂点，由
/// <see cref="GuTransformRankPatch"/>（裁决 D-04）单独补赋。
/// </para>
/// </summary>
internal static class GuDeckEntryRankPatch
{
    private const string HarmonyId = Entry.ModId + ".GuDeckEntryRank";

    // 与奖励/变形入口分离的独立随机流：各入口的随机推进互不影响，各自保持确定性。
    private const string RngStream = "deck_entry/gu_rank";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo shouldAddToDeck = RequiredMember.Method(
                typeof(Hook),
                nameof(Hook.ShouldAddToDeck),
                [
                    typeof(IRunState),
                    typeof(CardModel),
                    typeof(AbstractModel).MakeByRefType(),
                ]
            );

            harmony.Patch(
                shouldAddToDeck,
                postfix: new HarmonyMethod(
                    typeof(GuDeckEntryRankPatch),
                    nameof(ShouldAddToDeckPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    // 排在仙蛊唯一性仲裁（XianGuUniquenessPatch，Priority.Last）之后执行：只给最终
    // 放行入组的牌赋阶；被仲裁或原生结果拒绝的牌不消费随机流、不登记仙蛊。
    // 仲裁本身在赋阶前只看既有登记（候选此时仍是默认一转），不会把它当仙蛊；
    // 赋阶后若直接抽出六转以上，AssignInitialRank 内的封顶与登记保证唯一性不变。
    [HarmonyPriority(Priority.Last - 1)]
    private static void ShouldAddToDeckPostfix(
        CardModel card,
        ref bool __result
    )
    {
        if (!__result ||
            card is not AbstractGuCard gu ||
            !gu.NeedsInitialRankAssignment ||
            card.Owner is not { } player)
        {
            return;
        }

        GuRankRewardPatch.AssignInitialRank(gu, player, RngStream);

        Entry.Logger.Info(
            $"永久入组兜底：蛊牌 {gu.Id} 补抽初始转数 {gu.GuRank} 转。"
        );
    }
}
