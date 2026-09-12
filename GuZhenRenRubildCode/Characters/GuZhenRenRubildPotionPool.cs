using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

// 定义角色专属药水池的颜色和能量图标，即使暂时没有自定义药水也保留完整池结构。
public sealed class GuZhenRenRubildPotionPool : TypeListPotionPoolModel
{
    // EnergyColorName 与卡池、遗物池保持一致，让实验室和本地化组件能关联到同一套角色能量资源。
    public override string EnergyColorName => "GuZhenRenRubild";
    public override Color LabOutlineColor => GuZhenRenRubildCharacter.ThemeColor;

    // 即使模板暂时没有示例药水，也先把角色药水池结构留好。
    // AssetProfile 里的资源路径不存在时，RitsuLib 会输出诊断并回退；模板这里提供真实 PNG 占位。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
}
