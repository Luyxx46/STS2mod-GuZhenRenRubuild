using Godot;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 本模组卡池的公共父类：能量颜色、图标、轮廓色、边框材质与"非无色池"
/// 这类展示配置对所有卡池完全一致，子类只需要声明自己的 <c>Title</c>。
/// </summary>
public abstract class AbstractGuCardPool : TypeListCardPoolModel
{
    public override string EnergyColorName =>
        GuZhenRenRubildAssets.EnergyColorName;

    public override string? BigEnergyIconPath =>
        GuZhenRenRubildAssets.BigEnergyIconPath;

    public override string? TextEnergyIconPath =>
        GuZhenRenRubildAssets.TextEnergyIconPath;

    public override Color DeckEntryCardColor =>
        GuZhenRenRubildCharacter.ThemeColor;

    public override Color EnergyOutlineColor =>
        GuZhenRenRubildAssets.EnergyOutlineColor;

    public override Material? PoolFrameMaterial =>
        GuZhenRenRubildAssets.PoolFrameMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
