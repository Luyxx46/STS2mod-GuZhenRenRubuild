using System.Reflection;
using GuZhenRenRubild.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 把游戏原生战斗构建流程接入蛊牌生命周期：战斗状态建立后刷新蛊牌品阶派生值，正式开战后初始化专用牌堆。
/// </summary>
internal static class GuCombatPatch
{
    // 每组补丁使用独立 Harmony 编号，卸载时可以精确移除本组补丁。
    private const string HarmonyId = Entry.ModId + ".GuCombat";
    private static bool _initialized;

    // 查找目标方法并安装后置补丁；若游戏版本改变导致方法不存在，则立即抛出异常以暴露兼容性问题。
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo populateCombatState = AccessTools.DeclaredMethod(
            typeof(Player),
            nameof(Player.PopulateCombatState),
            [typeof(Rng), typeof(CombatState)]
        ) ?? throw new MissingMethodException(
            typeof(Player).FullName,
            nameof(Player.PopulateCombatState)
        );
        MethodInfo startCombat = AccessTools.DeclaredMethod(
            typeof(NetCombatCardDb),
            nameof(NetCombatCardDb.StartCombat),
            [typeof(IReadOnlyList<Player>)]
        ) ?? throw new MissingMethodException(
            typeof(NetCombatCardDb).FullName,
            nameof(NetCombatCardDb.StartCombat)
        );

        // PopulateCombatState 用于角色战斗状态构建；StartCombat 表示所有玩家即将进入正式战斗。
        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            populateCombatState,
            postfix: new HarmonyMethod(
                typeof(GuCombatPatch),
                nameof(PopulateCombatStatePostfix)
            )
        );
        harmony.Patch(
            startCombat,
            postfix: new HarmonyMethod(
                typeof(GuCombatPatch),
                nameof(StartCombatPostfix)
            )
        );
        _initialized = true;
    }

    // 移除当前 Harmony 编号安装的全部补丁，并允许后续重新初始化。
    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
    }

    // 原生战斗状态生成后，刷新抽牌堆、弃牌堆和手牌中的蛊牌派生数值。
    private static void PopulateCombatStatePostfix(Player __instance)
    {
        foreach (AbstractGuCard card in EnumerateNativeCombatPiles(__instance))
        {
            card.RefreshRankDerivedState();
        }
    }

    // 正式开战后为每名玩家建立蛊牌储备区、激活区和恢复区的初始布局。
    private static void StartCombatPostfix(IReadOnlyList<Player> players)
    {
        foreach (Player player in players)
        {
            GuCardPileSystem.InitializeCombat(player);
        }
    }

    // 这里只枚举原生三大牌堆，因为专用蛊牌堆会在后续 StartCombat 阶段统一初始化。
    private static IEnumerable<AbstractGuCard> EnumerateNativeCombatPiles(
        Player player
    )
    {
        return PileType.Draw.GetPile(player).Cards
            .Concat(PileType.Discard.GetPile(player).Cards)
            .Concat(PileType.Hand.GetPile(player).Cards)
            .OfType<AbstractGuCard>();
    }
}
