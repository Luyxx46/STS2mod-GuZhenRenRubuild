using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao;

/// <summary>
/// 辉光蛊（一转—二转 · 光道主线起点）。
///
/// <para>
/// A 形态弱直接：只造成等同于转数的伤害，真正的收益是给两张 <see cref="HuiGuangJian"/>
/// 各叠「转数」层【辉映】，把折光伤害抬起来。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class HuiGuangGu : AbstractGuAttackCard, ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：一至二转。</summary>
    public override int MaxGuRank => 2;

    public CompanionDefinition Companion => new(typeof(HuiGuangJian), 2);

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            // 一转给 1 层、二转给 2 层；辉映自身层数上限为 2。
            CompanionEnhancementGrant.Of<HuiYingEnhancement>(Math.Max(1, GuRank)),
        ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(1m, ValueProp.Move)];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.StrikeTemplate)
    );

    public HuiGuangGu()
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
        // 直接伤害 = 转数（一转 1 点，二转 2 点）；蛊不承担主要动作。
        DynamicVars.Damage.BaseValue = GuRank;
    }
}
