using Godot;

using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 本模组卡池的公共父类：能量颜色、图标、轮廓色、边框材质与"非无色池"
/// 这类展示配置对所有卡池完全一致，子类只需要声明自己的 <c>Title</c>。
///
/// 显式实现 <see cref="IModBigEnergyIconPool"/> / <see cref="IModTextEnergyIconPool"/>：
/// 让 RitsuLib 的 <c>EnergyIconHelperPathPatch</c> 把
/// <c>EnergyIconHelper.GetPath(EnergyColorName)</c> 解析到本模组的元气图标，
/// 而不是去找原版图集里不存在的 <c>energy_guzhenrenrubild.tres</c>。
/// 这两个接口声明的是"本池的能量图标用哪个自定义路径"，
/// 是模组自定义角色显示元气图标的标准入口。
/// </summary>
public abstract class AbstractGuCardPool :
    TypeListCardPoolModel,
    IModBigEnergyIconPool,
    IModTextEnergyIconPool
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
