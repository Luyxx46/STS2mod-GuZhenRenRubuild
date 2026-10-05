using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 聚光层数。
///
/// <para>
/// 聚光 X = 你拥有 X 次「折光重复机会」。它不监听伤害，也不自己结算：
/// 统一入口 <see cref="GuangDaoSystem.ResolveRefractionEffectAsync"/> 只在
/// <b>带数值的折光段</b>真正触发时消费 1 层，并把该折光段的结算次数从 1 变成 2。
/// 一次折光事件最多消耗 1 层，因此聚光 5 不会让同一张牌折 6 次，
/// 而是支持之后 5 次不同的数值型折光各重复一次。
/// </para>
///
/// <para>
/// 图标：沿用旧模组（STS2_GuZhenRen）的聚光图标——状态栏小图
/// <c>GuZhenRenRubild/images/powers/JuGuangPower-64x64.png</c>、悬浮提示大图
/// <c>.../JuGuangPower-256x256.png</c>（与旧模组 <c>PowerAssetProfile</c> 的拆分方式一致）。
/// 也可只放单文件 <c>.../JuGuangPower.png</c>；两者都缺失时暂借元气转盘第一层，避免状态栏出现空图标。
/// </para>
/// </summary>
[RegisterPower]
public sealed class JuGuangPower : ModPowerTemplate
{
    // 图标回退链：专属尺寸图 → 单文件命名 → 元气转盘第一层（128×128）。
    // 旧模组（STS2_GuZhenRen）的状态栏小图与悬浮大图是分开的两张：64×64 与 256×256，
    // 这里按同样的拆分用两个路径，避免把 256×256 塞进状态栏小图标槽位。
    // 注意：字段名不要叫 ResolvedBigIconPath——那会隐藏引擎 PowerModel 的同名公开属性（CS0108）。
    private static readonly string ResolvedIconTexturePath =
        ModAssetPathResolver.ResolveOptional(
            Entry.ResPath + "/images/powers/JuGuangPower-64x64.png",
            ModAssetPathResolver.ResolveOptional(
                Entry.ResPath + "/images/powers/JuGuangPower.png",
                Entry.ResPath + "/images/ui/orb1/1.png"
            )
        ) ?? Entry.ResPath + "/images/ui/orb1/1.png";

    private static readonly string ResolvedBigIconTexturePath =
        ModAssetPathResolver.ResolveOptional(
            Entry.ResPath + "/images/powers/JuGuangPower-256x256.png",
            ModAssetPathResolver.ResolveOptional(
                Entry.ResPath + "/images/powers/JuGuangPower.png",
                Entry.ResPath + "/images/ui/orb1/1.png"
            )
        ) ?? Entry.ResPath + "/images/ui/orb1/1.png";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: ResolvedIconTexturePath,
        BigIconPath: ResolvedBigIconTexturePath
    );
}
