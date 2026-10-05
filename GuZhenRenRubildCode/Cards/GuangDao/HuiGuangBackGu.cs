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
/// 回光蛊（二转—四转 · 光道奖励池）。
///
/// <para>
/// B 形态纯增幅：给 <see cref="HuiZhao"/> 挂【回照强化】，让被回收的攻击牌本回合减费。
/// 二转 1 次 -1 费、三转 2 次 -1 费、四转 2 次直接变为 0 费。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class HuiGuangBackGu
    : AbstractGuangDaoSupportGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：二至四转。</summary>
    public override int MaxGuRank => 4;

    /// <summary>回光蛊的冷却比常规蛊多 1 回合（回收是节奏型收益）。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(HuiZhao));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<HuiZhaoEnhancement>(
                amount: 1,
                // 四转起费用直接变为 0（magnitude 0 的约定），其余减 1。
                magnitude: GuRank >= 4 ? 0 : 1,
                // 二转只有 1 次，三转起 2 次。
                usesPerCombat: GuRank <= 2 ? 1 : 2
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public HuiGuangBackGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
