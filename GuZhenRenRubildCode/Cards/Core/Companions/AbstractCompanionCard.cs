using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>
/// 伴生牌仍是完整的普通卡牌：使用普通能量、普通抽牌体系和原生升级/附魔；
/// 它不属于蛊牌运行时，也不会由战斗内随机生成器凭空生成。
/// </summary>
public abstract class AbstractCompanionCard : ModCardTemplate, ICompanionCard
{
    public abstract Type SourceGuType { get; }

    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenRubildCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    protected AbstractCompanionCard(
        int energyCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    ) : base(energyCost, type, rarity, target, showInCardLibrary)
    {
    }
}
