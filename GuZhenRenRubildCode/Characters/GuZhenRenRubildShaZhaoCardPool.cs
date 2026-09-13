using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 杀招专属卡池：只收杀招牌。
///
/// 独立成池有两个作用：
/// 1. 配方注册表可以按池一次性扫描出全部杀招，而不必遍历其他内容池；
/// 2. 杀招不会混进蛊牌主奖励池，被普通卡牌奖励当作蛊牌发出。
///
/// 杀招的展示配置（能量色、图标、边框材质）与其余卡池完全一致，
/// 因此这里只声明自己的稳定标识。
/// </summary>
public sealed class GuZhenRenRubildShaZhaoCardPool : AbstractGuCardPool
{
    public override string Title => "GuZhenRenRubildShaZhao";
}
