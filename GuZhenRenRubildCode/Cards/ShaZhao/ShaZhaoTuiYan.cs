using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Characters;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRenRubild.Cards.ShaZhao;

/// <summary>
/// 杀招推演：空窍三转起，每场战斗开始时自动悬停于蛊手牌的系统牌。
///
/// 与仙元牌同一机制：不进入任何牌堆循环——不占起手抽牌、不占蛊牌补位槽
/// （蛊手牌容量只统计蛊牌），不会被弃置或消耗；战斗结束随战斗牌堆回收，
/// 下一场战斗重新发放，并固定排在蛊手牌最左端的仙元牌之后。
///
/// 自带催动次数：三转起每场 1 次，八转起 2 次；只有推演成功才消耗次数，
/// 次数耗尽后本牌保留在原地，但不可打出。
/// 打出后先选择要推演的杀招，再选择与该杀招匹配的蛊虫材料；
/// 材料合法且元气足够时，支付推演元气（等于材料元气消耗的平均值向上取整）
/// 与本牌的原生费用，把材料封装进蛊封存区并生成对应杀招加入手牌。
/// 取消、配方无效或资源不足时，原额返还原生管线已经支付的能量/星星，
/// 本牌留在原地，不消耗催动次数。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class ShaZhaoTuiYan : ModCardTemplate, IGuHandPinnedCard
{
    // 从未消耗过的牌用 -1 表示"满次数"，与仙元牌的余额状态同构；
    // 余额写在卡牌自身的 SavedAttachedState 上，随战斗快照与多人同步一起恢复。
    private const int UninitializedCharges = -1;

    private static readonly SavedAttachedState<CardModel, int> RemainingChargesState =
        new(Entry.ModId + ".sha_zhao_tui_yan_remaining_charges", static () => UninitializedCharges);

    public ShaZhaoTuiYan()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.None,
            showInCardLibrary: true
        )
    {
    }

    /// <summary>杀招推演牌固定悬停于蛊手牌最左端，排在仙元牌（秩 0）之后。</summary>
    public int GuHandOrderRank => 1;

    /// <summary>
    /// 系统牌注册在角色辅助卡池。
    /// 显式返回卡池，避免基类走全局卡池扫描回退（该回退会触发仅测试模式可用的卡池）。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenRubildCardPool>();

    /// <summary>
    /// 保留：本牌常驻蛊手牌，不会被弃置；关键词只用于向玩家说明这一点。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    /// <summary>系统牌不参与战斗内的随机生成，只能由空窍转数发放。</summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>系统牌不可升级。</summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>卡图统一使用 images/cards/ShaZhaoTuiYan.png。</summary>
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/ShaZhaoTuiYan.png"
    );

    /// <summary>
    /// 只还有剩余催动次数时才能打出；次数耗尽后本牌保留在原地但置灰。
    ///
    /// 这里刻意**不读 <c>Card.Pile</c>**：RitsuLib 为了让额外手牌复用原生打牌流程，
    /// 会在 <c>CardModel.CanPlay</c> 执行期间临时改写牌堆取值（见
    /// <see cref="GuCardRuntime.CanActivate"/> 的注释），按牌堆判定会直接判死。
    /// </summary>
    protected override bool IsPlayable => GetRemainingCharges(this) > 0;

    /// <summary>
    /// 打出后（无论推演成功或取消）回到蛊手牌原地：本牌不参与任何牌堆循环，
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

    /// <summary>把催动次数注入描述参数，卡面与蛊方大全说明都能读到同一份数值。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("MaxCharges", GetMaxCharges(this));
        description.Add("RemainingCharges", GetRemainingCharges(this));
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        // 一串连锁只处理第一张，避免复制效果重复触发推演。
        if (!cardPlay.IsFirstInSeries)
        {
            return;
        }

        bool succeeded = await ShaZhaoTuiYanSystem.DeriveAsync(
            choiceContext,
            Owner,
            cardPlay
        );

        if (succeeded)
        {
            // 只有成功才消耗催动次数；本牌随收尾声明回到蛊手牌原地，不再消耗。
            ConsumeCharge(this);
            return;
        }

        // 取消或失败：原额返还原生管线已经支付的资源，不消耗催动次数。
        //
        // 这里直接写回玩家战斗资源，而不走 PlayerCmd.GainEnergy / GainStars：
        // 退款不是"获得能量/星星"，走命令会误触发"每当你获得能量"一类的遗物或能力。
        PlayerCombatState? combatState = Owner.PlayerCombatState;

        if (combatState != null)
        {
            combatState.GainEnergy(cardPlay.Resources.EnergySpent);
            combatState.GainStars(cardPlay.Resources.StarsSpent);
        }

        if (cardPlay.Resources.EnergySpent > 0 ||
            cardPlay.Resources.StarsSpent > 0)
        {
            Entry.Logger.Info(
                "[杀招推演] 已回滚原生支付：" +
                $"能量 {cardPlay.Resources.EnergySpent}、" +
                $"星星 {cardPlay.Resources.StarsSpent}。"
            );
        }

        // 防御性归位：正常流程由 ModifyCardPlayResultLocation 声明回到蛊手牌，
        // 这里只在牌因异常落到别处时把它送回固定位。
        if (Pile?.Type != GuCardPileSystem.ActivePileType)
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                this,
                GuCardPileSystem.ActivePileType,
                skipVisuals: false
            );
        }
    }

    /// <summary>
    /// 本场战斗可催动次数：三转起 1 次，八转起 2 次（随空窍转数提升）。
    /// 读取不到空窍数据（图鉴预览等场景）时按 1 次处理。
    /// </summary>
    private static int GetMaxCharges(CardModel card)
    {
        if (card.Owner is not { } owner || !ApertureSystem.IsInitialized)
        {
            return 1;
        }

        return ApertureSystem.GetState(owner).Rank >=
                ApertureProgression.ShaZhaoDerivationSecondRank
            ? ApertureProgression.ShaZhaoDerivationMaxPerCombat
            : 1;
    }

    private static int GetRemainingCharges(CardModel card)
    {
        int max = GetMaxCharges(card);
        int saved = RemainingChargesState[card];
        return saved < 0 ? max : Math.Clamp(saved, 0, max);
    }

    private static void ConsumeCharge(CardModel card)
    {
        int remaining = GetRemainingCharges(card);
        RemainingChargesState[card] = Math.Max(0, remaining - 1);
    }
}
