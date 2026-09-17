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
/// 炫光蛊（二转—四转 · 光道材料/辅助）。
///
/// <para>
/// B 形态纯增幅：自身不产生攻防，催动给 <see cref="XuanGuangMu"/> 挂【炫光幕强化】，
/// 让它在接下来两次折光里额外贡献等同于转数的格挡，并把攻击侧的接续交出去。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class XuanGuangGu
    : AbstractGuangDaoSupportGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：二至四转。</summary>
    public override int MaxGuRank => 4;

    public CompanionDefinition Companion => new(typeof(XuanGuangMu));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<XuanGuangMuEnhancement>(
                amount: 1,
                magnitude: GuRank,
                usesPerCombat: 2
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public XuanGuangGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
