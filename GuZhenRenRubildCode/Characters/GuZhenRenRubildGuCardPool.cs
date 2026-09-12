using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 只包含真正蛊牌的角色主奖励池。普通初始牌、伴生牌和派生牌继续留在辅助池。
/// </summary>
public sealed class GuZhenRenRubildGuCardPool : TypeListCardPoolModel
{
    private static readonly Material? PoolFrameTintMaterial =
        MaterialUtils.CreateReplaceHueShaderMaterial(0.42f, 0.65f, 0.72f);

    public override string Title => "GuZhenRenRubildGu";
    public override string EnergyColorName => "GuZhenRenRubild";
    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";
    public override Color DeckEntryCardColor => GuZhenRenRubildCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;
    public override bool IsColorless => false;
}
