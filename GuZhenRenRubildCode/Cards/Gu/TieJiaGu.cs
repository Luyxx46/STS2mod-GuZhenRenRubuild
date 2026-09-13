using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu;

// 铁甲蛊：一转即可入手的防御型蛊牌，也是玄铁蛊的合练材料之一。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class TieJiaGu : AbstractGuBlockCard
{
    // 声明格挡动态变量；最终基础值会由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move)];

    // 暂时复用模板防御牌卡图，后续可替换为铁甲蛊专属资源。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildDefend.png"
    );

    public TieJiaGu()
        : base(CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        RefreshValues();
    }

    protected override void OnGuRankChanged()
    {
        RefreshValues();
    }

    private void RefreshValues()
    {
        // 格挡成长公式：基础 4 点，每提升 1 品阶额外增加 1 点格挡。
        DynamicVars.Block.BaseValue = 4m + GuRank;
    }
}
