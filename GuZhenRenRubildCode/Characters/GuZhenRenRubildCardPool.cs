using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Characters;

// 定义角色专属卡池的视觉主题与能量图标，所有注册到该池的卡牌都会继承这些展示配置。
public sealed class GuZhenRenRubildCardPool : TypeListCardPoolModel
{
    // 使用角色主题色创建卡池边框材质；静态缓存可避免每次读取属性时重复创建材质对象。
    private static readonly Material? PoolFrameTintMaterial =
        MaterialUtils.CreateRgbShaderMaterial(0.42f, 0.65f, 0.72f);

    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => "GuZhenRenRubild";
    public override string EnergyColorName => "GuZhenRenRubild";

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://GuZhenRenRubild/... 里的 GuZhenRenRubild 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    // 图鉴条目、能量轮廓和卡池边框使用统一色系，形成角色专属视觉识别。
    public override Color DeckEntryCardColor => GuZhenRenRubildCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
