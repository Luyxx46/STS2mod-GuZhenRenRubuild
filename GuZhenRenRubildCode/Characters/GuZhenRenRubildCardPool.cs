using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

// 辅助卡池：收普通初始牌与伴生牌。展示配置全部继承自 AbstractGuCardPool。
public sealed class GuZhenRenRubildCardPool : AbstractGuCardPool
{
    // Title 是池子的稳定标识，不是玩家看到的角色名。
    public override string Title => "GuZhenRenRubild";
}
