using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 仙元专属卡池：只收「仙元」货币牌（青提 / 红枣 / 白荔 / 黄杏）。
///
/// 独立成池有三个作用：
/// 1. 仙元牌是战斗内按空窍转数生成的货币，**不能**混进蛊牌主奖励池被当作蛊牌发出；
/// 2. 它们也不是蛊牌（不实现 <c>IGuCard</c>），奖励规则本来就会拦下，独立成池让这一点显式化；
/// 3. 界面（蛊方大全）按池枚举全部卡牌时能一次拿到这四张。
///
/// 展示配置（能量色、图标、边框材质）与其余卡池完全一致，因此这里只声明自己的稳定标识。
/// </summary>
public sealed class GuZhenRenRubildXianYuanCardPool : AbstractGuCardPool
{
    public override string Title => "GuZhenRenRubildXianYuan";
}
