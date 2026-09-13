using System.Runtime.CompilerServices;
using GuZhenRenRubild.Cards.Core.Runtime;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRenRubild.Cards.Core.Ui;

/// <summary>
/// 蛊手牌（RitsuLib ExtraHand，即激活区）的界面布局。
///
/// 保留 RitsuLib 原始的大卡与扇形排布，只把蛊手牌整体作为普通手牌后方的
/// 第二层手牌；并在鼠标离开普通手牌与蛊手牌的实际卡牌区域后，把两套手牌
/// 一起平滑下移，鼠标回到任一区域时再一起复位。
/// </summary>
internal static class GuHandLayoutSystem
{
    // 保留 RitsuLib 原始大卡与扇形布局，只把蛊手牌放到普通手牌后方。
    private static readonly Vector2 ExtraHandDownOffset = new(0f, 200f);

    // 鼠标离开普通手牌与蛊手牌区域时，两套手牌一起下移 40px；
    // 鼠标进入任意一套手牌区域时，两套一起恢复到现有位置。
    private const float HandAutoHideDistance = 40f;

    // 40px 约 0.125 秒完成，避免瞬移，同时保持响应足够快。
    private const float HandAutoHideSpeed = 320f;

    // 悬停只按每张卡真正接收鼠标输入的 Hitbox 判断；四周仅保留少量容错，
    // 避免 NPlayerHand / NModExtraHand 或 NHandCardHolder 自身的布局区域过大。
    private const float HandHoverPadding = 8f;

    // Hover 命中不需要跟随渲染帧率。30Hz 已足够跟手，同时把矩形与
    // Transform 检查从 60/120/144Hz 降下来。
    private const double HandHoverScanIntervalSeconds = 1d / 30d;

    // 手牌节点树只有在抽牌/弃牌等结构变化时才需要重新递归扫描。
    // 平时直接复用缓存的 Hitbox，避免每帧 GetChildren() 遍历整棵 UI 树。
    private const double HandHitboxCacheRefreshIntervalSeconds = 0.25d;

    // 蛊手牌与普通手牌贴齐同层，不再额外拉开 Z 间距。
    private const int ExtraHandZGap = 0;

    // 普通手牌（NPlayerHand）与蛊手牌（NModExtraHand）共用同一份布局基准状态：
    // 记录“完全展开”的基准位置、上一帧写入的位置，以及是否已做过首次布局。
    private sealed class HandLayoutState
    {
        internal bool HasApplied { get; set; }

        internal Vector2 BasePosition { get; set; }

        internal Vector2 AppliedPosition { get; set; }
    }

    private sealed class HandHoverCacheState
    {
        internal List<Control> Hitboxes { get; } = [];

        internal bool HasSnapshot { get; set; }

        internal double RefreshCountdown { get; set; }
    }

    private static readonly ConditionalWeakTable<NModExtraHand, HandLayoutState>
        ExtraHandLayoutStates = new();
    private static readonly ConditionalWeakTable<NPlayerHand, HandLayoutState>
        PrimaryHandLayoutStates = new();
    private static readonly ConditionalWeakTable<CanvasItem, HandHoverCacheState>
        HandHoverCacheStates = new();

    // 普通手牌与蛊手牌共用同一个收起偏移，确保两套界面始终同步移动。
    private static float _handAutoHideOffsetY;
    private static double _handHoverScanAccumulator = HandHoverScanIntervalSeconds;
    private static bool _cachedMouseOverAnyHand;

    /// <summary>
    /// 战斗结束或手牌节点销毁时复位收起偏移，避免下一场战斗继承上一场的
    /// 中间状态。
    /// </summary>
    internal static void Reset()
    {
        _handAutoHideOffsetY = 0f;
        _handHoverScanAccumulator = HandHoverScanIntervalSeconds;
        _cachedMouseOverAnyHand = false;
    }

    /// <summary>
    /// 保留 RitsuLib 原始 ExtraHand 卡牌布局，同时把本模组的蛊手牌放到普通
    /// 手牌后方。鼠标位于普通手牌或蛊手牌任一区域时，两套手牌都保持现有
    /// 位置；鼠标离开两套实际卡牌区域后，两套一起平滑下移 40px。悬停检测
    /// 逐张使用 <see cref="NHandCardHolder.Hitbox"/>，不使用 Holder 或手牌
    /// 容器自身的布局尺寸。状态对象分别记录原版与 RitsuLib 的布局基准，
    /// 避免逐帧重复叠加位移。
    /// </summary>
    internal static void UpdateExtraHandLayout(NModExtraHand extraHand, double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(extraHand);

        // 只接管本模组的蛊手牌；其他模组的额外手牌保持 RitsuLib 原生表现。
        if (extraHand.Definition.PileType != GuCardPileSystem.ActivePileType ||
            !GodotObject.IsInstanceValid(extraHand))
        {
            return;
        }

        double safeDeltaSeconds = Math.Max(0d, deltaSeconds);
        HandLayoutState state = ExtraHandLayoutStates.GetOrCreateValue(extraHand);

        Vector2 currentPosition = extraHand.Position;
        bool extraHandWasRelayout =
            !state.HasApplied ||
            !currentPosition.IsEqualApprox(state.AppliedPosition);

        if (extraHandWasRelayout)
        {
            // RitsuLib 初次布局或分辨率变化后给出的新基准位置。
            state.BasePosition = currentPosition;
        }

        NPlayerHand? primaryHand = NPlayerHand.Instance;
        if (primaryHand == null || !GodotObject.IsInstanceValid(primaryHand))
        {
            return;
        }

        HandLayoutState primaryState =
            PrimaryHandLayoutStates.GetOrCreateValue(primaryHand);

        if (!TryGetCanvasItemPosition(primaryHand, out Vector2 primaryCurrentPosition))
        {
            return;
        }

        bool primaryHandWasRelayout =
            !primaryState.HasApplied ||
            !primaryCurrentPosition.IsEqualApprox(primaryState.AppliedPosition);

        if (primaryHandWasRelayout)
        {
            // 原版手牌首次布局、分辨率变化或游戏自身重新布局后，
            // 记录最新的“完全展开”基准位置。
            primaryState.BasePosition = primaryCurrentPosition;
        }

        Vector2 extraExpandedPosition = state.BasePosition + ExtraHandDownOffset;

        // 只有首次布局或外部重新布局时，先恢复到当前自动收起偏移。
        // 正常帧不再先写“上一帧位置”再写“本帧位置”。
        if (extraHandWasRelayout)
        {
            Vector2 normalizedExtraPosition =
                extraExpandedPosition + Vector2.Down * _handAutoHideOffsetY;

            if (!extraHand.Position.IsEqualApprox(normalizedExtraPosition))
            {
                extraHand.Position = normalizedExtraPosition;
            }
        }

        // 这次写入并非与文件末尾的写入重复：紧随其后的 30Hz hover 扫描会读取
        // Hitbox 的全局变换，必须先让主手牌落到本帧实际展示位置再判断命中；
        // 末尾那次写入用的是本帧更新后的收起偏移，两者不可互相替代。
        if (primaryHandWasRelayout)
        {
            SetCanvasItemPosition(
                primaryHand,
                primaryState.BasePosition + Vector2.Down * _handAutoHideOffsetY
            );
        }

        // 卡牌 hover 检测固定为 30Hz；两套手牌的动画位移仍按实际帧率更新，
        // 所以不会把收起/展开动画降成 30FPS。
        _handHoverScanAccumulator += safeDeltaSeconds;
        if (_handHoverScanAccumulator >= HandHoverScanIntervalSeconds)
        {
            double hoverElapsed = _handHoverScanAccumulator;
            _handHoverScanAccumulator = 0d;

            Vector2 mousePosition = extraHand.GetGlobalMousePosition();
            bool isMouseOverExtraHand = IsMouseOverHandCards(
                extraHand,
                mousePosition,
                _handAutoHideOffsetY,
                hoverElapsed
            );
            bool isMouseOverPrimaryHand = IsMouseOverHandCards(
                primaryHand,
                mousePosition,
                _handAutoHideOffsetY,
                hoverElapsed
            );

            _cachedMouseOverAnyHand = isMouseOverExtraHand || isMouseOverPrimaryHand;
        }

        float targetAutoHideOffset = _cachedMouseOverAnyHand ? 0f : HandAutoHideDistance;
        _handAutoHideOffsetY = Mathf.MoveToward(
            _handAutoHideOffsetY,
            targetAutoHideOffset,
            HandAutoHideSpeed * (float)safeDeltaSeconds
        );

        Vector2 extraDesiredPosition =
            extraExpandedPosition + Vector2.Down * _handAutoHideOffsetY;

        if (!extraHand.Position.IsEqualApprox(extraDesiredPosition))
        {
            extraHand.Position = extraDesiredPosition;
        }

        SetCanvasItemPosition(
            primaryHand,
            primaryState.BasePosition + Vector2.Down * _handAutoHideOffsetY
        );

        state.AppliedPosition = extraDesiredPosition;
        state.HasApplied = true;

        primaryState.AppliedPosition =
            primaryState.BasePosition + Vector2.Down * _handAutoHideOffsetY;
        primaryState.HasApplied = true;

        // 以下属性只在值变化时写入，避免每帧把同一状态重新标脏。
        if (!extraHand.Scale.IsEqualApprox(Vector2.One))
        {
            extraHand.Scale = Vector2.One;
        }

        if (extraHand.Modulate != Colors.White)
        {
            extraHand.Modulate = Colors.White;
        }

        if (extraHand.ZAsRelative)
        {
            extraHand.ZAsRelative = false;
        }

        int desiredZIndex = Math.Max(
            -4000,
            GetEffectiveZIndex(primaryHand) - ExtraHandZGap
        );

        if (extraHand.ZIndex != desiredZIndex)
        {
            extraHand.ZIndex = desiredZIndex;
        }
    }

    /// <summary>
    /// 使用缓存的 <see cref="NHandCardHolder.Hitbox"/> 进行 hover 检测。缓存
    /// 低频刷新，抽牌/弃牌造成的节点变化最多约 0.25 秒后被发现；实际 hover
    /// 命中则以 30Hz 计算，避免逐帧递归遍历两套手牌节点树。
    /// </summary>
    private static bool IsMouseOverHandCards(
        CanvasItem handRoot,
        Vector2 mousePosition,
        float currentAutoHideOffsetY,
        double elapsedSeconds
    )
    {
        HandHoverCacheState cache = HandHoverCacheStates.GetOrCreateValue(handRoot);

        cache.RefreshCountdown -= elapsedSeconds;
        if (!cache.HasSnapshot || cache.RefreshCountdown <= 0d)
        {
            RebuildHandHitboxCache(handRoot, cache);
        }

        foreach (Control hitbox in cache.Hitboxes)
        {
            if (!GodotObject.IsInstanceValid(hitbox))
            {
                // 下次 hover 扫描立即重建，避免继续持有已释放节点。
                cache.RefreshCountdown = 0d;
                continue;
            }

            if (!hitbox.IsVisibleInTree() || hitbox.Size.X <= 0f || hitbox.Size.Y <= 0f)
            {
                continue;
            }

            Rect2 hitboxRect = GetControlGlobalAabb(hitbox);

            // hitboxRect 位于当前实际展示位置。收起时向上补回已经移动的距离，
            // 让鼠标仍能从原来的卡牌位置附近“叫回”手牌；同时向下补剩余行程，
            // 避免展开/收起过程中反复抖动。
            float topExtra = currentAutoHideOffsetY + HandHoverPadding;
            float bottomExtra =
                (HandAutoHideDistance - currentAutoHideOffsetY) + HandHoverPadding;

            Rect2 hoverRect = new(
                hitboxRect.Position - new Vector2(HandHoverPadding, topExtra),
                hitboxRect.Size + new Vector2(HandHoverPadding * 2f, topExtra + bottomExtra)
            );

            if (hoverRect.HasPoint(mousePosition))
            {
                return true;
            }
        }

        return false;
    }

    private static void RebuildHandHitboxCache(CanvasItem handRoot, HandHoverCacheState cache)
    {
        cache.Hitboxes.Clear();
        CollectHandHitboxesRecursive(handRoot, cache.Hitboxes);
        cache.HasSnapshot = true;
        cache.RefreshCountdown = HandHitboxCacheRefreshIntervalSeconds;
    }

    private static void CollectHandHitboxesRecursive(Node root, List<Control> hitboxes)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NHandCardHolder holder &&
                GodotObject.IsInstanceValid(holder) &&
                holder.IsNodeReady())
            {
                Control hitbox = holder.Hitbox;
                if (hitbox != null && GodotObject.IsInstanceValid(hitbox))
                {
                    hitboxes.Add(hitbox);
                }

                // NHandCardHolder 自身已提供最终 Hitbox，无需继续扫描它的
                // 卡面/文本/特效子树。
                continue;
            }

            CollectHandHitboxesRecursive(child, hitboxes);
        }
    }

    /// <summary>
    /// 使用真实 Hitbox 的四个角经过 GlobalTransform 后计算 AABB，因此卡牌
    /// 扇形布局带旋转时，检测仍紧贴实际点击区域。
    /// </summary>
    private static Rect2 GetControlGlobalAabb(Control control)
    {
        Transform2D transform = control.GetGlobalTransform();
        Vector2 size = control.Size;

        Vector2 p0 = transform * Vector2.Zero;
        Vector2 p1 = transform * new Vector2(size.X, 0f);
        Vector2 p2 = transform * new Vector2(0f, size.Y);
        Vector2 p3 = transform * size;

        float minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
        float minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
        float maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
        float maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

        return new Rect2(
            new Vector2(minX, minY),
            new Vector2(maxX - minX, maxY - minY)
        );
    }

    private static bool TryGetCanvasItemPosition(CanvasItem item, out Vector2 position)
    {
        switch (item)
        {
            case Control control:
                position = control.Position;
                return true;

            case Node2D node2D:
                position = node2D.Position;
                return true;

            default:
                position = Vector2.Zero;
                return false;
        }
    }

    private static void SetCanvasItemPosition(CanvasItem item, Vector2 position)
    {
        switch (item)
        {
            case Control control when !control.Position.IsEqualApprox(position):
                control.Position = position;
                break;

            case Node2D node2D when !node2D.Position.IsEqualApprox(position):
                node2D.Position = position;
                break;
        }
    }

    private static int GetEffectiveZIndex(CanvasItem item)
    {
        int zIndex = item.ZIndex;
        if (!item.ZAsRelative)
        {
            return zIndex;
        }

        Node? parent = item.GetParent();
        while (parent is CanvasItem parentCanvasItem)
        {
            zIndex += parentCanvasItem.ZIndex;
            if (!parentCanvasItem.ZAsRelative)
            {
                break;
            }

            parent = parentCanvasItem.GetParent();
        }

        return zIndex;
    }
}
