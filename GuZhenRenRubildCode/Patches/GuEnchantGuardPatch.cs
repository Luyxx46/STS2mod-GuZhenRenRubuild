using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 裁决 D-05/D-06（B）：拒绝**原版附魔**挂到蛊牌；伴生牌与普通牌保持可附魔不变。
///
/// <para>
/// 挂在 <see cref="EnchantmentModel.CanEnchant(CardModel)"/> 基类实现上：
/// 原生附魔基本只重写 <c>CanEnchantCardType</c> 而保留基类 <c>CanEnchant</c>，
/// 因此一处后缀即可覆盖几乎全部原生附魔与全部附魔类事件的选择/门槛
/// （FieldOfManSizedHoles、SapphireSeed、SelfHelpBook、Nonupeipe/Pael/Tanx 的
/// 「可附魔数量」计数等）。直接重写了 <c>CanEnchant</c> 的类型不受本补丁影响——
/// 本模组自己的强化载体 <see cref="AbstractCompanionEnhancement"/>（恒 false）已显式排除。
/// </para>
/// </summary>
internal static class GuEnchantGuardPatch
{
    private const string HarmonyId = Entry.ModId + ".GuEnchantGuard";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo canEnchant = RequiredMember.Method(
                typeof(EnchantmentModel),
                nameof(EnchantmentModel.CanEnchant),
                [typeof(CardModel)]
            );

            harmony.Patch(
                canEnchant,
                postfix: new HarmonyMethod(
                    typeof(GuEnchantGuardPatch),
                    nameof(CanEnchantPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        Host.Unpatch();
    }

    private static void CanEnchantPostfix(
        EnchantmentModel __instance,
        CardModel card,
        ref bool __result
    )
    {
        // 原流程已拒绝的保持拒绝；只把「将要放行到蛊牌」的原版附魔拦下。
        if (!__result ||
            card is not AbstractGuCard ||
            __instance is AbstractCompanionEnhancement)
        {
            return;
        }

        __result = false;
    }
}
