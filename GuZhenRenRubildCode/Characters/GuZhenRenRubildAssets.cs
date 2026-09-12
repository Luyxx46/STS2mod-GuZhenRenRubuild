using Godot;

using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Characters;

/// <summary>
/// 角色视觉资源的唯一来源：能量颜色标识、能量图标路径、能量轮廓色与卡池边框材质。
///
/// 卡池、遗物池、药水池、角色模型、元气副资源与元气表盘都从这里取值，
/// 避免同一个颜色或路径在多处各写一遍（改主题时只需要改这里）。
/// </summary>
public static class GuZhenRenRubildAssets
{
    /// <summary>四个内容池共用的能量颜色标识。</summary>
    public const string EnergyColorName = "GuZhenRenRubild";

    // 资源路径以 res:// 开头，并且要能在 PCK 内找到对应文件。
    public const string BigEnergyIconPath =
        $"{Entry.ResPath}/images/characters/energy_big.png";
    public const string TextEnergyIconPath =
        $"{Entry.ResPath}/images/characters/energy_text.png";

    // 卡牌文本与能量轮廓共用的深色描边。
    public static readonly Color EnergyOutlineColor = new(0.08f, 0.18f, 0.24f);

    // 卡池边框材质按角色主题色着色，首次使用时才创建，避免在 Godot 就绪前构造材质。
    private static readonly Lazy<Material?> FrameTintMaterial = new(
        static () => MaterialUtils.CreateReplaceHueShaderMaterial(
            GuZhenRenRubildCharacter.ThemeColor.R,
            GuZhenRenRubildCharacter.ThemeColor.G,
            GuZhenRenRubildCharacter.ThemeColor.B
        )
    );

    /// <summary>卡池边框的主题色材质，创建失败时为 null。</summary>
    public static Material? PoolFrameMaterial => FrameTintMaterial.Value;
}
