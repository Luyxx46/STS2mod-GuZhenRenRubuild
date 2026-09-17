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
/// 反光蛊（一转—三转 · 光道奖励池）。
///
/// <para>
/// B 形态纯增幅：长期防御密度的来源。催动给两张 <see cref="FanGuangBi"/> 各挂 1 次【返照】，
/// 让这次折光格挡额外 + 转数；每张反光壁本场只吃一次。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class FanGuangGu
    : AbstractGuangDaoSupportGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：一至三转。</summary>
    public override int MaxGuRank => 3;

    public CompanionDefinition Companion => new(typeof(FanGuangBi), 2);

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<FanZhaoEnhancement>(
                amount: 1,
                magnitude: GuRank,
                usesPerCombat: 1
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public FanGuangGu()
        : base(CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }
}
