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
/// 借光蛊（四转 · 光道材料/辅助）。
///
/// <para>
/// B 形态纯增幅：聚光蛊负责囤积，借光蛊负责<b>断档续接</b>——
/// 催动给 <see cref="JieGuangShu"/> 挂【借辉】，把接下来两次折光从
/// 「聚光为 0 才补 1 层」改成「无条件补 1 层」。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class JieGuangGu
    : AbstractGuangDaoSupportGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：四转。</summary>
    public override int MaxGuRank => 4;

    public CompanionDefinition Companion => new(typeof(JieGuangShu));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<JieHuiEnhancement>(
                amount: 1,
                magnitude: 1,
                usesPerCombat: 2
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public JieGuangGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
