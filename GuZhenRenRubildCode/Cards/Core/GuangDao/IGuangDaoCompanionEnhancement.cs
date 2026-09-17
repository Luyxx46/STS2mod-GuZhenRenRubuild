namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 光道伴生强化对「折光段」的附加值。
///
/// <para>
/// 这些数值全部由强化自身在<b>折光真正触发时</b>提供，卡牌只读取、不自行推导；
/// 因此折光段被聚光重复时，附着其上的强化也会跟着一起再次结算
/// （这正是设计里「强化会被重复」的语义）。
/// </para>
///
/// <para>
/// 默认实现全部为 0 / false，具体强化只需覆写自己用得到的那几项。
/// </para>
/// </summary>
public interface IGuangDaoCompanionEnhancement
{
    /// <summary>
    /// 折光段伤害加成。<paramref name="passIndex"/> 是本次折光的第几遍
    /// （0 = 正常结算，1 = 聚光重复出来的那一遍），用于表达「只在重复那遍加伤」的强化。
    /// </summary>
    decimal RefractionDamageBonus(int passIndex) => 0m;

    /// <summary>折光段格挡加成（两遍都吃）。</summary>
    decimal RefractionBlockBonus => 0m;

    /// <summary>折光触发时额外获得的聚光层数（纯功能折光的附加值，不因此改变折光分类）。</summary>
    int ExtraJuGuangOnRefraction => 0;

    /// <summary>折光触发后是否使下一张带折光段的牌强制折光。</summary>
    bool ForcesNextRefraction => false;

    /// <summary>
    /// 是否在「本次折光因聚光发生了重复」时额外返还 1 层聚光并令下一张牌强制折光
    /// （恒照）。卡面自己判断重复是否真的发生，强化只负责提供开关与消耗次数。
    /// </summary>
    bool RewardsRepeatedRefraction => false;

    /// <summary>
    /// 回照类强化：被回收牌的费用减免量；<c>0</c> 表示「本回合费用变为 0」，
    /// <c>-1</c> 表示本强化不提供费用减免。
    /// </summary>
    int RecoverCostReduction => -1;
}
