using GuZhenRenRubild.Cards.Core.Abstractions;

namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>
/// 可选能力：实现该接口的蛊牌拥有若干张永久伴生普通牌。
///
/// <para>
/// 继承方只需实现 <see cref="Companion"/>（主伴生）。需要「一只蛊带两种不同伴生」
/// （例如恒光仙蛊带恒光刃 + 恒光障）时，覆写 <see cref="CompanionDefinitions"/>
/// 并让 <see cref="Companion"/> 返回其中的第一项即可，配平、强化与联机映射全部自动泛用。
/// </para>
/// </summary>
public interface ICompanionSourceGuCard : IGuCard
{
    /// <summary>主伴生定义（单伴生蛊的唯一伴生；多伴生蛊的第一个）。</summary>
    CompanionDefinition Companion { get; }

    /// <summary>
    /// 该来源蛊带出的全部伴生定义。缺省实现为「只有主伴生一项」，
    /// 因此现有单伴生蛊牌不需要任何改动。
    /// </summary>
    IReadOnlyList<CompanionDefinition> CompanionDefinitions => [Companion];
}
