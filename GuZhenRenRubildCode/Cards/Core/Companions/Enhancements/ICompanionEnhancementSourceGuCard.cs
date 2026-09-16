using GuZhenRenRubild.Cards.Core.Companions;

namespace GuZhenRenRubild.Cards.Core.Companions.Enhancements;

/// <summary>
/// 可选能力：实现该接口的蛊牌（必须同时是 <see cref="ICompanionSourceGuCard"/> 伴生来源）
/// 每次被催动时，会按 <see cref="BuildCompanionEnhancementGrant"/> 的声明自动给伴生牌挂强化。
///
/// <para>
/// 分工与 <see cref="ICompanionSourceGuCard"/> 一致——具体蛊牌只做声明：
/// 给什么强化、本次几层（层数可读 <c>GuRank</c> 等自身状态自定义推导，
/// 对应原版 <c>CardCmd.Enchant</c> 的自定义 amount）；催动管线
/// （<c>GuZhenRenRubildRelic.BeforeCardPlayed</c>）在蛊牌正式结算前统一调用
/// <see cref="CompanionEnhancementService.ApplyDeclaredGrants"/>，具体蛊牌不写任何挂载代码。
/// </para>
///
/// <para>
/// 未实现本接口的蛊牌催动时零开销（一次类型判断后直接返回）；
/// 授予目标通过 <c>CompanionRelationshipService.GetCompanions</c> 解析为
/// 配对内的全部伴生牌，多伴生来源（<see cref="CompanionDefinition.Count"/> &gt; 1）天然泛用。
/// </para>
/// </summary>
public interface ICompanionEnhancementSourceGuCard : ICompanionSourceGuCard
{
    /// <summary>
    /// 构建本次催动授予伴生牌的强化声明。
    /// 返回 null 表示本次催动不挂强化，可用于条件授予（例如只在特定转数窗口生效）。
    /// </summary>
    CompanionEnhancementGrant? BuildCompanionEnhancementGrant();
}
