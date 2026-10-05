using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-16（方案 B）：烟雾/昏眩等原版苦难之力阻断蛊牌时的人物气泡提示。
///
/// <para>
/// 原版点击路径（NMouseCardPlay/NControllerCardPlay）在 CanPlay 失败后把
/// <see cref="UnplayableReason"/> 翻成一句人物台词（GetPlayerDialogueLine）弹出思考气泡；
/// 苦难之力阻断（BlockedByHook，preventer 为 PowerModel）的台词是笼统的
/// "被 XX 阻止"（BLOCKED_BY_HOOK），玩家看不出"元气充足为什么点不动这张蛊"。
/// 本补丁在阻断者是烟雾弥漫（SmoggyPower）或昏眩（RingingPower）且被拦的是蛊牌时，
/// 把台词换成蛊语境的专门说明（参照 <see cref="XianYuanWarningPatch"/> 的手法，
/// 文案放在原版既有表 combat_messages 里）。
/// </para>
///
/// <para>
/// GetPlayerDialogueLine 的参数里没有被拦的卡，因此先 postfix
/// <c>CardModel.CanPlay(out reason, out preventer)</c> 把"被苦难之力拦下的蛊牌"
/// 暂存进 AsyncLocal 上下文——CanPlay 与台词查询在点击路径上是同步连续调用，
/// 无并发交错；台词 postfix 消费即清除，CanPlay postfix 每次先重置。
/// </para>
/// </summary>
internal static class GuAfflictionWarningPatch
{
    private const string HarmonyId = Entry.ModId + ".GuAfflictionWarning";

    // 与原版 UnplayableReasonExtensions 使用同一张表：combat_messages 是原版既有的战斗台词表。
    private const string DialogueTable = "combat_messages";
    private const string SmogKey = "GU_ZHEN_REN_RUBILD_GU_SMOG_BLOCKED";
    private const string RingingKey = "GU_ZHEN_REN_RUBILD_GU_RINGING_BLOCKED";

    // 暂存"最近一次被苦难之力拦下的蛊牌"。AsyncLocal 随异步链流动，不会串扰其他玩家流程。
    private static readonly AsyncLocal<CardModel?> BlockedGu = new();

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo canPlay = RequiredMember.Method(
                typeof(CardModel),
                nameof(CardModel.CanPlay),
                [
                    typeof(UnplayableReason).MakeByRefType(),
                    typeof(AbstractModel).MakeByRefType(),
                ]
            );

            MethodInfo getLine = RequiredMember.DeclaredMethod(
                typeof(UnplayableReasonExtensions),
                "GetPlayerDialogueLine",
                [typeof(UnplayableReason), typeof(AbstractModel)]
            );

            // 挂载前校验签名：签名变了说明游戏换了实现方式，宁可报错也不要静默失效。
            if (getLine.ReturnType != typeof(LocString))
            {
                throw new MissingMethodException(
                    $"{nameof(UnplayableReasonExtensions)}.GetPlayerDialogueLine " +
                    "签名与预期不符（返回类型不是 LocString）。"
                );
            }

            harmony.Patch(
                canPlay,
                postfix: new HarmonyMethod(
                    typeof(GuAfflictionWarningPatch),
                    nameof(CanPlayPostfix)
                )
            );
            harmony.Patch(
                getLine,
                postfix: new HarmonyMethod(
                    typeof(GuAfflictionWarningPatch),
                    nameof(GetPlayerDialogueLinePostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch(static () => BlockedGu.Value = null);
    }

    // 记录"被苦难之力拦下的蛊牌"：只有 BlockedByHook 且阻断者是烟雾弥漫/昏眩之力时才暂存，
    // 元气/关键词等其它一切不可打出原因不碰台词（保持原版文案）。
    private static void CanPlayPostfix(
        CardModel __instance,
        bool __result,
        UnplayableReason reason,
        AbstractModel? preventer
    )
    {
        BlockedGu.Value = null;

        if (__result ||
            !reason.HasFlag(UnplayableReason.BlockedByHook) ||
            preventer is not (SmoggyPower or RingingPower) ||
            __instance is not IGuCard)
        {
            return;
        }

        BlockedGu.Value = __instance;
    }

    // 消费暂存的蛊牌上下文，把笼统的"被 XX 阻止"换成蛊语境说明。
    private static void GetPlayerDialogueLinePostfix(
        UnplayableReason reason,
        AbstractModel? preventer,
        ref LocString? __result
    )
    {
        CardModel? blocked = BlockedGu.Value;

        if (blocked == null)
        {
            return;
        }

        // 消费即清除：同一次点击只会查询一次台词。
        BlockedGu.Value = null;

        if (!reason.HasFlag(UnplayableReason.BlockedByHook))
        {
            return;
        }

        __result = preventer switch
        {
            SmoggyPower => new LocString(DialogueTable, SmogKey),
            RingingPower => new LocString(DialogueTable, RingingKey),
            _ => __result,
        };
    }
}
