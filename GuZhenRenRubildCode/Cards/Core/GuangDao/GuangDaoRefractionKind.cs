namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 折光段的机制分类。
///
/// <para>
/// 分类由<b>卡牌基础折光段</b>决定，临时强化不改变分类
/// （催动蛊给纯功能折光附加的数值只当作「强化附加值」，见 <see cref="GuangDaoCardPlay"/>）。
/// </para>
/// </summary>
public enum GuangDaoRefractionKind
{
    /// <summary>该牌没有折光段。</summary>
    None = 0,

    /// <summary>
    /// 数值型折光：折光段带伤害或格挡数值。
    /// 会被聚光重复（整段额外结算一次），一次折光事件最多消耗 1 层聚光。
    /// </summary>
    Numeric = 1,

    /// <summary>
    /// 纯功能折光：折光段只有抽牌、回复元气、获得聚光、强制折光、回收等功能。
    /// 既不消耗聚光，也不会被聚光重复。
    /// </summary>
    Functional = 2,
}

/// <summary>强制折光的生效范围。</summary>
public enum GuangDaoForceScope
{
    /// <summary>不强制。</summary>
    None = 0,

    /// <summary>下一张带光道标签的牌（任意类型）强制折光。</summary>
    AnyGuangDaoCard = 1,

    /// <summary>下一张带光道标签的攻击牌强制折光；非攻击的光道牌会跳过而不消费标记。</summary>
    AttackCardOnly = 2,
}
