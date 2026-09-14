using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.ImmortalEssence;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 「没有仙元」的人物气泡提醒。
///
/// 六转及以上的蛊牌在仙元不足时不可打出：原版点击路径
/// （<c>NMouseCardPlay</c> / <c>NControllerCardPlay</c>）在 <c>CanPlay</c> 失败后会把
/// <see cref="UnplayableReason"/> 翻成一句人物台词（<c>GetPlayerDialogueLine</c>）并弹出思考气泡。
/// 本补丁在那句台词上做后缀覆盖：只要被拦下的是一张**六转及以上的蛊牌**且玩家**仙元确实不够**，
/// 就把台词换成专门的仙元提示（放在原版既有表 <c>combat_messages</c> 里），
/// 而不是原版那句笼统的"不能打出"。
///
/// 旧模组没有这层提示（只有"点不动 + 日志"），这是本仓库新增的能力。
///
/// 已知取舍（复审 XY-1）：当一张六转以上蛊牌因多种原因同时不可打出
/// （如次数耗尽 + 仙元不足）时，本补丁仍优先报仙元不足——
/// 仙元确属真实缺口即覆盖台词，不逐一区分首要原因，属有意为之。
/// </summary>
internal static class XianYuanWarningPatch
{
    private const string HarmonyId = Entry.ModId + ".XianYuanWarning";

    // 与原版 UnplayableReasonExtensions 使用同一张表：combat_messages 是原版既有的战斗台词表。
    private const string DialogueTable = "combat_messages";
    private const string DialogueKey =
        "GU_ZHEN_REN_RUBILD_NO_IMMORTAL_ESSENCE";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo target = RequiredMember.DeclaredMethod(
            typeof(UnplayableReasonExtensions),
            "GetPlayerDialogueLine",
            [typeof(UnplayableReason), typeof(AbstractModel)]
        );

        // 挂载前校验签名：签名变了说明游戏换了实现方式，宁可报错也不要静默失效。
        if (target.ReturnType != typeof(LocString))
        {
            throw new MissingMethodException(
                $"{nameof(UnplayableReasonExtensions)}.GetPlayerDialogueLine " +
                "签名与预期不符（返回类型不是 LocString）。"
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            target,
            postfix: new HarmonyMethod(
                typeof(XianYuanWarningPatch),
                nameof(GetPlayerDialogueLinePostfix)
            )
        );
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
    }

    /// <summary>
    /// 只接管"卡牌自身逻辑拦下"的场合：元气/星星/关键词等其它原因一律放行，
    /// 保持原版文案不变。
    /// </summary>
    private static void GetPlayerDialogueLinePostfix(
        UnplayableReason reason,
        AbstractModel? preventer,
        ref LocString? __result
    )
    {
        if (!reason.HasFlag(UnplayableReason.BlockedByCardLogic) &&
            !reason.HasFlag(UnplayableReason.HasUnplayableKeyword))
        {
            return;
        }

        if (preventer is not CardModel card ||
            card is not IGuCard gu ||
            card.Owner is not { } owner)
        {
            return;
        }

        int cost = ImmortalEssenceSystem.GetActivationCost(gu.GuRank);

        if (cost <= 0 ||
            ImmortalEssenceSystem.GetAvailableUnits(owner) >= cost)
        {
            return;
        }

        __result = new LocString(DialogueTable, DialogueKey);
    }
}
