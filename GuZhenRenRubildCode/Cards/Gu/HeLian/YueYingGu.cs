using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Gu;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.HeLian;

// 月影蛊：由两张月光蛊，或血煞蛊配铁甲蛊合练而成。
// 结果转数沿用默认策略，取全部材料中的最高转数。
// 月光蛊位于 Cards/Basic（目录名与类名同名），命名空间与类型同名，因此用 global:: 全限定名引用。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(global::GuZhenRenRubild.Cards.Basic.YueGuangGu.YueGuangGu),
    typeof(global::GuZhenRenRubild.Cards.Basic.YueGuangGu.YueGuangGu)
)]
[HeLianRecipe(typeof(XueShaGu), typeof(TieJiaGu), MinimumMaterialRank = 2)]
public sealed class YueYingGu : AbstractHeLianGuCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(11m, ValueProp.Move)];

    // 合练蛊的冷却比普通蛊更长，作为高数值的代价。
    public override int RecoveryDelayTurns => 3;

    // 暂时复用模板打击牌卡图，后续可替换为月影蛊专属资源。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildStrike.png"
    );

    public YueYingGu()
        : base(CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        RefreshValues();
    }

    // 合练蛊不能继承攻击型蛊牌父类，这里直接复用公共出牌动作。
    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.AttackAsync(
        this,
        choiceContext,
        cardPlay,
        GuCardPlay.DefaultAttackHitFx
    );

    protected override void OnGuRankChanged()
    {
        RefreshValues();
    }

    private void RefreshValues()
    {
        // 伤害成长公式：基础 9 点，每提升 1 品阶额外增加 2 点伤害。
        DynamicVars.Damage.BaseValue = 9m + GuRank * 2m;
    }
}
