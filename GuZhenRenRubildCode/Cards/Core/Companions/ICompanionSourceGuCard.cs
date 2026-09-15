using GuZhenRenRubild.Cards.Core.Abstractions;

namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>
/// 可选能力：实现该接口的蛊牌拥有若干张永久伴生普通牌，
/// 数量由 <see cref="CompanionDefinition.Count"/> 自定义（缺省 1 张）。
/// </summary>
public interface ICompanionSourceGuCard : IGuCard
{
    CompanionDefinition Companion { get; }
}
