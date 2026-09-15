using GuZhenRenRubild.Cards.Core.ImmortalEssence;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Abstractions;

/// <summary>
/// 「仙元」货币牌的公共父类。
///
/// 四种仙元（青提 / 红枣 / 白荔 / 黄杏）只有面额不同，行为完全一致：
/// - 0 费、不可升级；
/// - 战斗开始时由空窍按转数发放到**蛊手牌堆**，并一直停在原地（不冷却、不进待命堆、不弃牌）；
/// - 主动打出：消耗 1 个催动单位，把元气补满；
/// - 六转及以上的蛊牌催动时，由 <see cref="ImmortalEssenceSystem"/> 从这里扣减单位；
/// - 单位耗尽后本牌仍然留在蛊手牌堆，但既不能打出也不能再被消耗。
///
/// 与旧模组的两处刻意差异（按当前需求设计）：
/// 1. 旧版放在**普通手牌**并靠 <c>Retain</c> 跨回合保留；本版直接停在蛊手牌堆的固定位；
/// 2. 旧版 `IsPlayable => false` 且耗尽即 `Exhaust` 消失；本版可主动打出，且耗尽后保留成死牌。
/// </summary>
public abstract class AbstractXianYuanCard : ModCardTemplate, IGuHandPinnedCard
{
    protected AbstractXianYuanCard()
        : base(
            0,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self,
            showInCardLibrary: true
        )
    {
    }

    /// <summary>仙元牌固定悬停于蛊手牌最左端。</summary>
    public int GuHandOrderRank => 0;

    /// <summary>本档仙元牌一共提供多少个"催动单位"。</summary>
    public abstract int ActivationUnits { get; }

    /// <summary>本档仙元牌对应的空窍转数（六转起），用于发放与说明文案。</summary>
    public abstract int GrantedApertureRank { get; }

    /// <summary>仙元牌只由空窍在战斗开始时发放，不参与战斗内的随机生成。</summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>仙元牌不参与原生升级：面额固定，升级没有意义。</summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>停在蛊手牌堆里跨回合保留；关键词只用于向玩家说明这一点。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    /// <summary>归属仙元专属隐藏卡池，避免被当作蛊牌或普通奖励牌处理。</summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenRubildXianYuanCardPool>();

    /// <summary>
    /// 只有还有催动单位时才能打出。
    ///
    /// 这里刻意**不读 <c>Card.Pile</c>**：RitsuLib 为了让额外手牌复用原生打牌流程，
    /// 会在 <c>CardModel.CanPlay</c> 执行期间临时改写牌堆取值（见
    /// <see cref="GuCardRuntime.CanActivate"/> 的注释），按牌堆判定会直接判死。
    /// </summary>
    protected override bool IsPlayable =>
        ImmortalEssenceSystem.GetRemainingUnits(this) > 0;

    // 卡图按类名解析：images/cards/{类名}.png（四张图先沿用旧模组美术）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    /// <summary>
    /// 打出后回到蛊手牌堆：本牌不参与蛊牌的使用次数与冷却循环，
    /// 因此这里直接声明归属，而不是走 <c>GuCardRuntime.GetResultPile</c>。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location
    )
    {
        if (ReferenceEquals(card, this))
        {
            location.pileType = GuCardPileSystem.ActivePileType;
        }

        return location;
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => ImmortalEssenceSystem.PlayEssenceAsync(this, choiceContext, cardPlay);

    /// <summary>把余额注入描述参数，卡面与蛊方大全说明都能读到同一份数值。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("ActivationUnits", ActivationUnits);
        description.Add(
            "RemainingActivationUnits",
            ImmortalEssenceSystem.GetRemainingUnits(this)
        );
    }
}
