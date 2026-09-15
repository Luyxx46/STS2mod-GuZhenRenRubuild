namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>声明一张蛊牌所依赖的普通伴生牌类型与数量。</summary>
/// <param name="CardType">伴生牌类型，必须实现 <see cref="ICompanionCard"/>。</param>
/// <param name="Count">
/// 该来源进入牌组时自动获得的伴生牌张数，最小按 1 处理；
/// 缺省为 1，保持旧的单伴生行为不变。
/// </param>
public sealed record CompanionDefinition(Type CardType, int Count = 1);
