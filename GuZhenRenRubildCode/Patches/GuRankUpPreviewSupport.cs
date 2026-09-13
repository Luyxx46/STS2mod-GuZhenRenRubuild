using System.Reflection;
using System.Runtime.CompilerServices;

using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Rules;
using GuZhenRenRubild.Common.Text;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Patches;

/// <summary>
/// 为篝火升炼提供原生升级预览范围，并把升转后新增或变化的卡面内容
/// 标成绿色。范围外不会改变原生升级效果对蛊虫的限制。
///
/// 本类只负责"预览作用域 + 资格判定 + 数值标记"；
/// 文本差异染色属于通用文本能力，位于 <see cref="BbCodeDiff"/>。
/// </summary>
internal static class GuRankUpPreviewSupport
{
    private static readonly object ScopeLock = new();

    private static ConditionalWeakTable<
        CardModel,
        PreviewState
    > _previewStates = new();

    private static PreviewContext? _activeContext;

    internal static bool IsActive
    {
        get
        {
            lock (ScopeLock)
            {
                return _activeContext != null;
            }
        }
    }

    private sealed class PreviewContext(
        int remainingSlots,
        IEnumerable<CardModel> excludedCards
    )
    {
        internal int RemainingSlots { get; } = remainingSlots;

        internal HashSet<CardModel> ExcludedCards { get; } =
            new(excludedCards);
    }

    private sealed record PreviewState(string BeforeDescription);

    internal static void PatchUpgradeDescription(Harmony harmony)
    {
        MethodInfo? method = AccessTools.DeclaredMethod(
            typeof(CardModel),
            nameof(CardModel.GetDescriptionForUpgradePreview)
        );

        if (method == null)
        {
            throw new MissingMethodException(
                "升炼绿色差异预览所需的卡牌描述方法不存在。"
            );
        }

        harmony.Patch(
            method,
            postfix: new HarmonyMethod(
                typeof(GuRankUpPreviewSupport),
                nameof(GetUpgradeDescriptionPostfix)
            )
        );
    }

    internal static IDisposable Begin(
        int remainingSlots,
        IEnumerable<CardModel> excludedCards
    )
    {
        if (remainingSlots is < 1 or > GuRankUpRules.SlotBudget)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remainingSlots)
            );
        }

        PreviewContext current = new(
            remainingSlots,
            excludedCards
        );
        PreviewContext? previous;

        lock (ScopeLock)
        {
            previous = _activeContext;
            _activeContext = current;
        }

        return new PreviewScope(current, previous);
    }

    internal static bool TryGetIsUpgradable(
        CardModel card,
        out bool result
    )
    {
        PreviewContext? context = GetActiveContext();
        if (context == null)
        {
            result = false;
            return false;
        }

        result = card is AbstractGuCard gu &&
            IsEligible(gu, context, checkExcluded: true);
        return true;
    }

    internal static bool TryIncreaseForPreview(
        AbstractGuCard gu
    )
    {
        PreviewContext? context = GetActiveContext();
        if (context == null ||
            !IsEligible(gu, context, checkExcluded: false))
        {
            return false;
        }

        string beforeDescription = gu.GetDescriptionForPile(
            PileType.None
        );
        Dictionary<string, decimal> beforeValues =
            gu.DynamicVars.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.BaseValue,
                StringComparer.Ordinal
            );

        if (!gu.TryIncreaseGuRank())
        {
            return false;
        }

        MarkChangedDynamicVars(gu, beforeValues);

        _previewStates.Remove(gu);
        _previewStates.Add(
            gu,
            new PreviewState(beforeDescription)
        );
        return true;
    }

    internal static void Reset()
    {
        lock (ScopeLock)
        {
            _activeContext = null;
        }

        _previewStates = new();
    }

    private static void GetUpgradeDescriptionPostfix(
        CardModel __instance,
        ref string __result
    )
    {
        if (__instance is not AbstractGuCard ||
            !_previewStates.TryGetValue(
                __instance,
                out PreviewState? state
            ))
        {
            return;
        }

        __result = BbCodeDiff.HighlightAddedOrChanged(
            state.BeforeDescription,
            __result
        );
    }

    private static void MarkChangedDynamicVars(
        CardModel card,
        IReadOnlyDictionary<string, decimal> beforeValues
    )
    {
        foreach ((string name, DynamicVar variable) in card.DynamicVars)
        {
            if (beforeValues.TryGetValue(name, out decimal before) &&
                before != variable.BaseValue)
            {
                // 数值已经由升转逻辑写入；增加 0 只设置原生
                // WasJustUpgraded 标记，使 :diff() 按绿色显示。
                variable.UpgradeValueBy(0);
            }
        }
    }

    private static bool IsEligible(
        AbstractGuCard gu,
        PreviewContext context,
        bool checkExcluded
    )
    {
        if (checkExcluded && context.ExcludedCards.Contains(gu))
        {
            return false;
        }

        int slotCost = GuRankUpRules.GetSlotCost(gu);

        return slotCost <= context.RemainingSlots &&
            GuRankUpRules.CanRankUp(gu);
    }

    private static PreviewContext? GetActiveContext()
    {
        lock (ScopeLock)
        {
            return _activeContext;
        }
    }

    private sealed class PreviewScope(
        PreviewContext current,
        PreviewContext? previous
    ) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (ScopeLock)
            {
                if (ReferenceEquals(_activeContext, current))
                {
                    _activeContext = previous;
                }
            }

            _disposed = true;
        }
    }
}
