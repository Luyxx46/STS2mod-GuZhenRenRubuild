using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Characters;

// 定义角色专属遗物池的主题颜色和能量图标，供遗物实验室及相关文本界面读取。
public sealed class GuZhenRenRubildRelicPool : TypeListRelicPoolModel
{
    // 与卡池、药水池共享同一个能量颜色标识，确保角色内容在各类实验室界面中保持一致。
    public override string EnergyColorName => "GuZhenRenRubild";
    public override Color LabOutlineColor => GuZhenRenRubildCharacter.ThemeColor;

    // 遗物实验室和文本也会读取池子的能量图标路径。
    // 资源路径以 res:// 开头，并且要能在 PCK 内找到对应文件。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
}
