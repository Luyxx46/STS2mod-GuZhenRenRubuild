using GuZhenRenRubild.Cards.Core;

namespace GuZhenRenRubild.Cards.Companions;

/// <summary>可选能力：实现该接口的蛊牌拥有一张永久伴生普通牌。</summary>
public interface ICompanionSourceGuCard : IGuCard
{
    CompanionDefinition Companion { get; }
}
