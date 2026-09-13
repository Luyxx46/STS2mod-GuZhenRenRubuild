using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu;

// 血煞蛊：一转即可入手的攻击型蛊牌，也是月影蛊的合练材料之一。
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class XueShaGu : AbstractGuAttackCard
{
    // 声明伤害动态变量；最终基础值会由品阶公式在 RefreshValues 中覆盖。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5m, ValueProp.Move)];

    // 血煞蛊的命中特效比默认斩击更血腥。
    protected override string HitFxPath => "vfx/vfx_bloody_impact";

    // 暂时复用模板打击牌卡图，后续可替换为血煞蛊专属资源。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildStrike.png"
    );

    public XueShaGu()
        : base(CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        RefreshValues();
    }

    protected override void OnGuRankChanged()
    {
        RefreshValues();
    }

    private void RefreshValues()
    {
        // 伤害成长公式：基础 4 点，每提升 1 品阶额外增加 1 点伤害。
        DynamicVars.Damage.BaseValue = 4m + GuRank;
    }
}
