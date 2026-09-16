using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Basic.YueGuangGu;

// 将「月光蛊」注册进角色卡池，并作为 2 张初始蛊牌加入角色初始牌组。
// 它同时是伴生来源：进入牌组后由 CompanionRelationshipService 自动带出对应数量的月刃。
// 与伴生牌 YueGuangCompanion 同处一个目录：一只初始蛊连同它的伴生牌自成一组。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuYueFangYuan), 2)]
public sealed class YueGuangGu : AbstractGuAttackCard, ICompanionSourceGuCard
{
    // 目录名与类名同为 YueGuangGu，命名空间因此与本类同名，跨目录引用必须走 global:: 全限定名。
    public CompanionDefinition Companion =>
        new(typeof(global::GuZhenRenRubild.Cards.Basic.YueGuangGu.YueGuangCompanion));

    // 声明伤害动态变量；最终基础值由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move)];

    // 暂时复用模板打击牌卡图，后续可替换为月光蛊专属卡图。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildStrike.png"
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
        // 伤害成长公式：基础 5 点，每提升 1 品阶额外增加 1 点伤害。
        DynamicVars.Damage.BaseValue = 5m + GuRank;
    }
}
