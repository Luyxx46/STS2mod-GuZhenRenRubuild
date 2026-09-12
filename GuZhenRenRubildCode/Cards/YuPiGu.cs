using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards;

// 将“玉皮蛊”注册进角色卡池，并作为 1 张初始蛊牌加入角色初始牌组。
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenRubildCharacter), 1)]
public sealed class YuPiGu : AbstractGuCard
{
    // 声明格挡动态变量；最终基础值会由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(8m, ValueProp.Move)];

    // 告诉游戏该卡会获得格挡，供提示、统计及其他规则系统识别。
    public override bool GainsBlock => true;

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

    // 打出后按动态变量中的最终格挡值为角色获得格挡。
    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
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
