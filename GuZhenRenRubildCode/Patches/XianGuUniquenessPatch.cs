using System.Reflection;

using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 仙蛊唯一性的入牌仲裁。
///
/// 卡牌进入永久牌组时，<c>CardPileCmd.Add</c> 会调用 <c>Hook.ShouldAddToDeck</c>
/// （全游戏唯一调用点），这里在其结果上补一次唯一性检查：候选牌是仙蛊且整局中
/// 已有同名仙蛊时拒绝入牌；候选优先级更高时，由 <see cref="GuXianGuRules"/>
/// 把落败的既有同名仙蛊降回五转。
///
/// 该补丁覆盖奖励领取、商店购买、事件转换、合练结果等所有永久入牌路径，
/// 是升转仲裁之外的第二道（也是最后一道）闸门。
/// </summary>
internal static class XianGuUniquenessPatch
{
    private const string HarmonyId = Entry.ModId + ".XianGuUniqueness";

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
                    typeof(XianGuUniquenessPatch),
                    nameof(ShouldAddToDeckPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    // 排在最后：先尊重游戏本体与其他模型的拒绝结果；只有原流程允许时，
    // 才执行会写入仙蛊仲裁状态的最终检查。
    [HarmonyPriority(Priority.Last)]
    private static void ShouldAddToDeckPostfix(
        IRunState runState,
        CardModel card,
        ref bool __result,
        ref AbstractModel? preventer
    )
    {
        if (!__result)
        {
            return;
        }

        if (GuXianGuRules.TryAuthorizePermanentDeckEntry(runState, card))
        {
            return;
        }

        // 与游戏本体一致：把卡牌自身登记为阻止者，让原生流程走
        // AfterAddToDeckPrevented 的失败收尾，而不是静默丢牌。
        preventer = card;
        __result = false;
    }
}
