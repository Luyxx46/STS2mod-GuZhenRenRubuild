using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Basic.YueGuangGu;

// 将「月光蛊」注册进角色卡池，并作为 2 张初始蛊牌加入角色初始牌组。
// 它没有伴生牌：自身只是一转窗口的低伤攻击蛊，价值全在篝火被合练成月霓裳蛊。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuYueFangYuan), 2)]
public sealed class YueGuangGu : AbstractGuAttackCard
{
    // 月系起点只有一转窗口：它不靠升炼成长，而是在篝火被合练成月霓裳蛊。
    public override int MaxGuRank => 1;

    // 声明伤害动态变量；最终基础值由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(2m, ValueProp.Move)];

    // 专属卡图按类名解析：images/cards/YueGuangGu.png 存在就用，缺失时回退模板打击图。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    // 月光蛊是以任意敌人为目标的普通攻击牌，构造完成后立即同步当前品阶伤害。
    public YueGuangGu()
        : base(CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        RefreshValues();
    }

    // 品阶变化时同步刷新伤害，保证卡面显示和实际结算使用同一数值。
    protected override void OnGuRankChanged()
    {
        RefreshValues();
    }

    private void RefreshValues()
    {
        // 月光蛊只保留极弱的直接伤害（固定 2 点）：它的价值是与玉皮蛊的合练配方，
        // 以及作为月系起点牌进入初始牌组。
        DynamicVars.Damage.BaseValue = 2m;
    }
}
