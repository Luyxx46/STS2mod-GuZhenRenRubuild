using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao;

/// <summary>
/// 光源蛊（一转—三转 · 光道材料/辅助）。
///
/// <para>
/// B 形态纯增幅：催动给 <see cref="YinGuang"/> 挂【引光强化】，
/// 让它在接下来两次折光里额外贡献等同于转数的格挡。
/// 引光自身的折光段是纯功能的「回复 1 元气」，不会因此变成数值型折光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class GuangYuanGu
    : AbstractGuangDaoSupportGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：一至三转。</summary>
    public override int MaxGuRank => 3;

    public CompanionDefinition Companion => new(typeof(YinGuang));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<YinGuangEnhancement>(
                amount: 1,
                magnitude: GuRank,
                usesPerCombat: 2
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public GuangYuanGu()
        : base(CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }
}
