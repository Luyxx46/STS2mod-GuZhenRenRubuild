using System.Reflection;

using Godot;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Common.Patching;
using GuZhenRenRubild.Common.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 让蛊牌的费用区域显示"元气"而不是原生能量。
///
/// 蛊牌的元气费用保存在 RitsuLib 的副资源费用表里（见
/// <see cref="AbstractGuCard"/> 构造函数中的 <c>SecondaryCosts().Set(...)</c>），
/// 不写入 <see cref="CardModel.EnergyCost"/>——蛊牌的原生能量费用固定为 0。
/// 因此原版 <c>NCard.UpdateEnergyCostVisuals</c> 只会把那个 0 画到费用位置，
/// 玩家看不到这张蛊实际要消耗几点元气。
///
/// 补丁形式与旧模组（STS2_GuZhenRen 的 <c>NCardGuEnergyIconPatch</c>）保持一致：
/// 在原版刷新完费用显示之后，对蛊牌改写费用数字、费用图标与配色；
/// 非蛊牌（或尚未解锁、显示 "?" 的卡）则还原成原版外观。
/// 本补丁只改本地 <see cref="NCard"/> 节点，不读写成名战斗状态，也不影响联机同步数据。
/// </summary>
internal static class NCardGuEnergyCostPatch
{
    private const string HarmonyId = Entry.ModId + ".NCardGuEnergyCost";

    // 标记"这张卡的费用区已经被本补丁按元气改过色"。
    // NCard 节点会在不同卡牌之间复用，CostVisuals 也会被高频刷新，
    // 用 meta 记录状态才能既避免重复写颜色，又能在换成非蛊牌时准确还原。
    private const string AppliedMeta = "GuZhenRenRubildYuanQiCostApplied";

    // 本补丁组的生命周期（幂等初始化 / 反初始化）由共用 Host 持有，与手写 _initialized 等价。
    private static readonly HarmonyPatchHost Host = new(HarmonyId);

    // 费用图标与费用数字在 NCard 上都是私有字段，只能在初始化时反射取得。
    private static FieldInfo? _energyIconField;
    private static FieldInfo? _energyLabelField;

    internal static void Initialize()
    {
        Host.TryInitialize(static harmony =>
        {
            MethodInfo updateEnergyCostVisuals = RequiredMember.DeclaredMethod(
                typeof(NCard),
                "UpdateEnergyCostVisuals",
                [typeof(PileType)]
            );

            _energyIconField = RequiredMember.Field(typeof(NCard), "_energyIcon");
            _energyLabelField = RequiredMember.Field(typeof(NCard), "_energyLabel");

            harmony.Patch(
                updateEnergyCostVisuals,
                postfix: new HarmonyMethod(
                    typeof(NCardGuEnergyCostPatch),
                    nameof(UpdateEnergyCostVisualsPostfix)
                )
            );
        });
    }

    internal static void Uninitialize()
    {
        // 与手写 try/finally 等价：先解除补丁，再清掉反射缓存，最后置回未初始化。
        Host.Unpatch(ResetReflectionState);
    }

    // 原版画完能量费用后再覆盖成元气费用。放到 postfix 是必须的：
    // 原版会在 UpdateEnergyCostVisuals 内部按牌堆与可支付状态重设文字与颜色，
    // 任何更早的写入都会被它覆盖掉。
    private static void UpdateEnergyCostVisualsPostfix(NCard __instance)
    {
        if (_energyIconField?.GetValue(__instance) is not TextureRect energyIcon ||
            _energyLabelField?.GetValue(__instance) is not MegaLabel energyLabel)
        {
            return;
        }

        if (__instance.Visibility != ModelVisibility.Visible ||
            __instance.Model is not IGuCard guCard)
        {
            // 非蛊牌：把图标还原成原版白色并清掉标记，其余显示交还原版逻辑。
            if (energyIcon.HasMeta(AppliedMeta))
            {
                energyIcon.SelfModulate = Colors.White;
                energyIcon.RemoveMeta(AppliedMeta);
            }

            return;
        }

        energyLabel.SetTextAutoSize(ResolveYuanQiCostText(guCard));
        energyLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            GuZhenRenRubildAssets.YuanQiCostTextColor
        );
        energyLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            GuZhenRenRubildAssets.EnergyOutlineColor
        );

        // 图标显式写回 Model.EnergyIcon：卡池解析出来的就是本模组的元气图标，
        // 并用淡蓝灰色着色与原生能量区分开。
        energyIcon.Texture = __instance.Model.EnergyIcon;
        energyIcon.SelfModulate = GuZhenRenRubildAssets.YuanQiCostIconTint;
        energyIcon.Visible = true;
        energyIcon.SetMeta(AppliedMeta, true);
    }

    // 取本张蛊牌要支付的元气点数。
    //
    // 直接读蛊牌自己声明的 <see cref="IGuCard.YuanQiCost"/>：它既是
    // <c>SecondaryCosts()</c> 写进 RitsuLib 支付表的数值，也是
    // <see cref="Cards.Core.Runtime.GuCardRuntime.CanActivate"/> 判定能否催动的数值，
    // 因此卡面显示与真正扣除、能否打出三者必然一致。
    // 旧模组这里改读 RitsuLib 的支付计划，是因为它额外实现了光道减费等元气修正；
    // 本仓库目前没有任何元气修正入口，声明值就是真实值，
    // 也就不需要在每次卡面刷新时构造支付计划（避免高频刷新下的额外分配）。
    // 将来若加入元气减费/免费系统，这里是唯一需要改成读支付计划的地方。
    private static string ResolveYuanQiCostText(IGuCard guCard) =>
        Math.Max(0, guCard.YuanQiCost).ToString();

    private static void ResetReflectionState()
    {
        _energyIconField = null;
        _energyLabelField = null;
    }
}
