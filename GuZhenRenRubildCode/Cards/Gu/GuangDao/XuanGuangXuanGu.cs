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
/// 璇光蛊（四转—五转 · 光道奖励池）。
///
/// <para>
/// B 形态纯增幅：攻技混合 + 抽牌循环 + 少量聚光。
/// 催动给 <see cref="XuanGuangRen"/> 与 <see cref="XuanGuangMu2"/> 各挂 1 次【璇照】，
/// 使两张伴生这次折光后各额外获得 1 聚光——两张伴生自身的折光是纯功能抽牌，
/// 因此抽牌本身不会被成倍复制。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
public sealed class XuanGuangXuanGu
    : AbstractGuangDaoSupportGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>转数窗口上限：四至五转。</summary>
    public override int MaxGuRank => 5;

    /// <summary>璇光蛊催动需要 2 点元气。</summary>
    public override int YuanQiCost => 2;

    /// <summary>璇光蛊冷却 3 回合。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(XuanGuangRen));

    // 一条来源带两种伴生：璇光刃（攻击）＋ 璇光幕（技能）。
    public IReadOnlyList<CompanionDefinition> CompanionDefinitions =>
        [new(typeof(XuanGuangRen)), new(typeof(XuanGuangMu2))];

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<XuanZhaoEnhancement>(
                amount: 1,
                magnitude: 1,
                usesPerCombat: 1
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public XuanGuangXuanGu()
        : base(CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }
}
