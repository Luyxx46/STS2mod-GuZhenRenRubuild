using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-04（A+补赋）：允许原版变形消耗蛊牌，但变形**产出的蛊牌**必须补赋初始转数，
/// 不允许停在默认的一转。
///
/// <para>
/// 原版 <see cref="CardCmd.Transform"/> 对蛊牌的替换牌取自蛊牌池
/// （<c>CardFactory.GetDefaultTransformationOptions</c> ⇒ <c>original.Pool</c>），
/// 因此结果仍是蛊牌；且牌组变形走 <c>AddInternal</c>，**不经过**
/// <c>Hook.ShouldAddToDeck</c> 的仙蛊唯一性仲裁。所以本补丁在变形完成后：
/// 对每张成功产出、尚未获得初始品阶的蛊牌按奖励同款规则补赋
/// （含「已有同名仙蛊 ⇒ 上限压到五转」的封顶与成仙楼层登记，见
/// <see cref="GuRankRewardPatch.AssignInitialRank"/>），堵住「变形绕过唯一性」的口子。
/// </para>
///
/// <para>
/// 补赋语义 = 把变形结果当作「新获得的一只蛊」随机定阶（与卡牌奖励一致，均值随楼层
/// 缓慢提高），而不是迁移原蛊转数——这正是裁决里「补赋」的含义。
/// </para>
/// </summary>
internal static class GuTransformRankPatch
{
    private const string HarmonyId = Entry.ModId + ".GuTransformRank";

    // 与奖励入口分离的独立随机流：两个入口的随机推进互不影响，各自保持确定性。
    private const string RngStream = "transform/gu_rank";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo transform = RequiredMember.Method(
                typeof(CardCmd),
                nameof(CardCmd.Transform),
                [
                    typeof(IEnumerable<CardTransformation>),
                    typeof(MegaCrit.Sts2.Core.Random.Rng),
                    typeof(CardPreviewStyle),
                ]
            );

            harmony.Patch(
                transform,
                postfix: new HarmonyMethod(
                    typeof(GuTransformRankPatch),
                    nameof(TransformPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static void TransformPostfix(
        ref Task<IEnumerable<CardPileAddResult>> __result
    )
    {
        __result = AssignTransformedGuRanksAsync(__result);
    }

    private static async Task<IEnumerable<CardPileAddResult>>
        AssignTransformedGuRanksAsync(
            Task<IEnumerable<CardPileAddResult>> transformTask
        )
    {
        IEnumerable<CardPileAddResult> results = await transformTask;

        foreach (CardPileAddResult result in results)
        {
            if (!result.success ||
                result.cardAdded is not AbstractGuCard gu ||
                !gu.NeedsInitialRankAssignment)
            {
                continue;
            }

            if (gu.Owner is not { } player)
            {
                continue;
            }

            GuRankRewardPatch.AssignInitialRank(gu, player, RngStream);

            Entry.Logger.Info(
                $"原版变形产出蛊牌 {gu.Id}，已补赋初始转数 {gu.GuRank} 转。"
            );
        }

        return results;
    }
}
