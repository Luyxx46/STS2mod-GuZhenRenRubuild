using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Basic.YuPiGu;

// 将「玉皮蛊」注册进角色卡池，并作为 1 张初始蛊牌加入角色初始牌组。
// 它没有伴生牌：自身只是一转窗口的低格挡技能蛊，价值全在篝火被合练成月霓裳蛊。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuYueFangYuan), 1)]
public sealed class YuPiGu : AbstractGuBlockCard
{
    // 玉皮蛊只有一转窗口：它是月系合练的通用防御材料，成长交给月霓裳一系。
    public override int MaxGuRank => 1;

    // 声明格挡动态变量；最终基础值会由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(2m, ValueProp.Move)];

    // 专属卡图按类名解析：images/cards/YuPiGu.png 存在就用，缺失时回退模板防御图。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    // 玉皮蛊是以自身为目标的普通技能牌，构造完成后立即按当前品阶刷新格挡值。
    public YuPiGu()
        : base(CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        RefreshValues();
    }

    // 品阶发生变化或从存档恢复后，重新计算所有依赖品阶的卡牌数值。
    protected override void OnGuRankChanged()
    {
        RefreshValues();
    }

    private void RefreshValues()
    {
        // 玉皮蛊只保留最低限度的直接格挡（固定 2 点）：它的价值是与月光蛊的合练配方，
        // 以及作为月系起点牌进入初始牌组。
        DynamicVars.Block.BaseValue = 2m;
    }
}
