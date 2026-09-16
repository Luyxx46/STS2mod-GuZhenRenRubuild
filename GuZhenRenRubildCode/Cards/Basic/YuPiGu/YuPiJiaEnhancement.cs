using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.Basic.YuPiGu;

/// <summary>
/// 「玉皮甲强化」：父蛊 <see cref="YuPiGu"/>（玉皮蛊）每次催动时，给它的子卡
/// <see cref="YuPiCompanion"/>（玉皮甲）叠 1 层。
///
/// <para>
/// 每层使玉皮甲**获得的格挡 +3**（层数即 <c>Amount</c>，上限 <see cref="MaxAmount"/>）。
/// </para>
///
/// <para>
/// 注意原版钩子的契约：<c>EnchantBlockAdditive</c> 返回的是**增量**而不是「原值 + 增量」
/// ——前置分发侧是 <c>result += 钩子(...)</c>，返回原值+增量会双倍计算。
/// </para>
/// </summary>
[RegisterEnchantment]
public sealed class YuPiJiaEnhancement : AbstractCompanionEnhancement
{
    // 每层提升的格挡；数值改动只需动这一处常量。
    private const decimal BlockPerStack = 3m;

    // 强化层数上限：三次催动封顶（服务层挂载时会按此上限夹取层数）。
    public override int MaxAmount => 3;

    // 卡面追加一行说明，让玩家在子卡上直接看到强化效果。
    public override bool HasExtraCardText => true;

    public override decimal EnchantBlockAdditive(decimal originalBlock) =>
        BlockPerStack * Amount;
}
