using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Companions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards;

// 将“月光蛊”注册进角色卡池，并作为 2 张初始蛊牌加入角色初始牌组。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenRubildCharacter), 2)]
public sealed class YueGuangGu : AbstractGuCard, ICompanionSourceGuCard
{
    public CompanionDefinition Companion => new(typeof(YueGuangCompanion));

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

    // 仅在目标存在且通过卡牌自身目标校验时发动攻击，避免联机或自动流程传入无效目标。
    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (cardPlay.Target == null || !IsValidTarget(cardPlay.Target))
        {
            return;
        }

        // 使用当前基础伤害创建攻击命令，并绑定本次卡牌打出上下文和斩击命中特效。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
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
