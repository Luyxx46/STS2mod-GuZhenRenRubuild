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
/// 图标：把 <c>GuZhenRenRubild/images/powers/JuGuangPower.png</c> 放进资源目录即自动生效；
/// 缺失时暂借元气转盘第一层，避免状态栏出现空图标。
/// </para>
/// </summary>
[RegisterPower]
public sealed class JuGuangPower : ModPowerTemplate
{
    // 专属图标缺失时的临时图：元气转盘第一层（128×128）。
    private static readonly string ResolvedIconPath =
        ModAssetPathResolver.ResolveOptional(
            Entry.ResPath + "/images/powers/JuGuangPower.png",
            Entry.ResPath + "/images/ui/orb1/1.png"
        )!;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: ResolvedIconPath,
        BigIconPath: ResolvedIconPath
    );
}
