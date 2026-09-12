using GuZhenRenRubild.Cards.Core;
using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu;

// 将“玉皮蛊”注册进角色卡池，并作为 1 张初始蛊牌加入角色初始牌组。
// 它同时是伴生来源：进入牌组后由 CompanionRelationshipService 自动带出 1 张玉皮甲。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenRubildCharacter), 1)]
public sealed class YuPiGu : AbstractGuBlockCard, ICompanionSourceGuCard
{
    public CompanionDefinition Companion => new(typeof(YuPiCompanion));

    // 声明格挡动态变量；最终基础值会由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(8m, ValueProp.Move)];

    // 暂时复用模板防御牌卡图，后续可直接替换为玉皮蛊专属资源。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildDefend.png"
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
        // 格挡成长公式：基础 6 点，每提升 1 品阶额外增加 2 点格挡。
        DynamicVars.Block.BaseValue = 6m + GuRank * 2m;
    }
}
