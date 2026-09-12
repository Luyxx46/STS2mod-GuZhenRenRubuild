namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>标识由伴生关系系统管理的普通卡牌。</summary>
public interface ICompanionCard
{
    Type SourceGuType { get; }
}
