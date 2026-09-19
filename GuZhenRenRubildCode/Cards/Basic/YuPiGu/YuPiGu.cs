using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Basic.YuPiGu;

// 将「玉皮蛊」注册进角色卡池，并作为 1 张初始蛊牌加入角色初始牌组。
// 它同时是伴生来源：进入牌组后由 CompanionRelationshipService 自动带出 3 张玉皮甲。
// 与伴生牌 YuPiCompanion 同处一个目录：一只初始蛊连同它的伴生牌自成一组。
// 作为「父蛊」，它每次催动还会按声明给子卡（玉皮甲）叠一层 YuPiJiaEnhancement。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuYueFangYuan), 1)]
public sealed class YuPiGu : AbstractGuBlockCard, ICompanionEnhancementSourceGuCard
{
    // 目录名与类名同为 YuPiGu，命名空间因此与本类同名，跨目录引用必须走 global:: 全限定名。
    // 数量取自设计案工作簿「蛊虫设计」的伴生结构列（玉皮甲×3）。
    public CompanionDefinition Companion =>
        new(typeof(global::GuZhenRenRubild.Cards.Basic.YuPiGu.YuPiCompanion), 3);

    // 玉皮蛊只有一转窗口：它是月系合练的通用防御材料，成长交给月霓裳一系。
    public override int MaxGuRank => 1;

    // 父蛊声明：每次催动给子卡叠 1 层玉皮甲强化（层数上限由强化自身的 MaxAmount 封顶）。
    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [CompanionEnhancementGrant.Of<YuPiJiaEnhancement>(1)];

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
        // 玉皮蛊只保留最低限度的直接格挡（固定 2 点）。
        DynamicVars.Block.BaseValue = 2m;
    }
}
