using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-09（C）：原版苦难（Hexed/Entangled/Smog/Ringing 等）只允许命中
/// **激活区（蛊手牌）**里的蛊牌；储备/恢复/封存区的休眠蛊牌一律免疫。
///
/// <para>
/// 原版四个施加源（HexPower/TangledPower/SmoggyPower/RingingPower）都通过
/// <see cref="CardCmd.Afflict"/> 落苦难，因此在这一个入口拦截即可全覆盖
/// （含 AfterApplied 的整批遍历与 AfterCardEnteredCombat 的逐张补挂）。
/// 激活区判定同时接受 <see cref="PileType.Hand"/>：蛊牌出牌事务期间会被临时
/// 挪进原生手牌（见 GuZhenRenRubildRelic.BeforeCardPlayed），这一窗口内仍算"在场"。
/// </para>
///
/// <para>
/// 清理侧无需对称处理：<c>CardCmd.ClearAffliction</c> 对没有苦难的牌是 no-op，
/// Power 移除时的批量清理对从未挂上的休眠蛊牌天然无害。
/// 激活区内挂上的苦难仍按原版语义生效（Smog/Ringing 可阻断催动、Entangled 加费、
/// Hexed 提供惰性 Ethereal），对应裁决 D-16 的提示问题尚未裁决、本补丁不处理。
/// </para>
/// </summary>
internal static class GuAfflictionScopePatch
{
    private const string HarmonyId = Entry.ModId + ".GuAfflictionScope";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo afflict = RequiredMember.Method(
                typeof(CardCmd),
                nameof(CardCmd.Afflict),
                [
                    typeof(AfflictionModel),
                    typeof(CardModel),
                    typeof(decimal),
                ]
            );

            // 该重载是同步方法（内部全部 Task.FromResult），前缀可以直接改写结果。
            if (afflict.ReturnType != typeof(Task<AfflictionModel?>))
            {
                throw new MissingMethodException(
                    $"{nameof(CardCmd)}.{nameof(CardCmd.Afflict)} " +
                    "签名与预期不符（返回类型不是 Task<AfflictionModel?>）。"
                );
            }

            harmony.Patch(
                afflict,
                prefix: new HarmonyMethod(
                    typeof(GuAfflictionScopePatch),
                    nameof(AfflictPrefix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static bool AfflictPrefix(
        CardModel card,
        ref Task<AfflictionModel?> __result
    )
    {
        if (card is not AbstractGuCard)
        {
            return true;
        }

        PileType? pileType = card.Pile?.Type;
        if (pileType == GuCardPileSystem.ActivePileType ||
            pileType == PileType.Hand)
        {
            return true;
        }

        // 休眠区（储备/恢复/封存，以及战斗外的任何位置）的蛊牌不吃原版苦难：
        // 对调用方表现为「施加失败」（返回 null），与原版 CanAfflict 拒绝时的行为一致。
        __result = Task.FromResult<AfflictionModel?>(null);
        return false;
    }
}
