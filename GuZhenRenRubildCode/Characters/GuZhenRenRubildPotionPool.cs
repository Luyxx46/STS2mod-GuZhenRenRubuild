using Godot;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

// 定义角色专属药水池的颜色和能量图标，即使暂时没有自定义药水也保留完整池结构。
public sealed class GuZhenRenRubildPotionPool : TypeListPotionPoolModel
{
    // EnergyColorName 与卡池、遗物池保持一致，让实验室和本地化组件能关联到同一套角色能量资源。
    public override string EnergyColorName =>
        GuZhenRenRubildAssets.EnergyColorName;

    public override Color LabOutlineColor =>
        GuZhenRenRubildCharacter.ThemeColor;

    public override string? BigEnergyIconPath =>
        GuZhenRenRubildAssets.BigEnergyIconPath;

    public override string? TextEnergyIconPath =>
        GuZhenRenRubildAssets.TextEnergyIconPath;
}
