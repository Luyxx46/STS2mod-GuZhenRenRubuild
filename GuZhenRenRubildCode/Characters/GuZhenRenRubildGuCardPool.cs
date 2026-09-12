using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 只包含真正蛊牌的角色主奖励池。普通初始牌、伴生牌和派生牌继续留在辅助池。
/// </summary>
public sealed class GuZhenRenRubildGuCardPool : AbstractGuCardPool
{
    public override string Title => "GuZhenRenRubildGu";
}
