using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Core.ShaZhao;
using GuZhenRenRubild.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.ShaZhao;

/// <summary>
/// 杀招推演：空窍三转起，每场战斗开始时加入手牌的系统牌。
///
/// 打出后先选择要推演的杀招，再选择与该杀招匹配的蛊虫材料；
/// 材料合法且元气足够时，支付推演元气（等于材料元气消耗的平均值向上取整）
/// 与本牌的原生费用，把材料封装进蛊封存区并生成对应杀招加入手牌，
/// 本牌随后消耗。取消、配方无效或资源不足时本牌回到手牌，
/// 并原额返还原生管线已经支付的能量/星星。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class ShaZhaoTuiYan : ModCardTemplate
{
    // 记录本次打出是否以"取消/失败"收场，供返回牌堆位置时使用。
    private bool _derivationReturnedToHand;

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

    /// <summary>
    /// 系统牌注册在角色辅助卡池。
    /// 显式返回卡池，避免基类走全局卡池扫描回退（该回退会触发仅测试模式可用的卡池）。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenRubildCardPool>();

    /// <summary>
    /// 保留：取消推演后继续留在手牌。
    /// 成功推演后由 OnPlay 主动消耗，因此不声明"消耗"关键词，
    /// 避免取消或失败时被误消耗。
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
    /// 推演失败时把本牌留在手牌。
    ///
    /// OnPlay 里已经主动移动过一次，这里再按状态声明一次目标牌堆：
    /// 原生播放收尾会按本方法决定去向，两条路径都指向手牌，
    /// 因此无论收尾时机先后都不会把本牌丢进弃牌堆。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location
    )
    {
        if (ReferenceEquals(card, this) && _derivationReturnedToHand)
        {
            location.pileType = PileType.Hand;
        }

        return location;
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
            _derivationReturnedToHand = false;
            await CardCmd.Exhaust(
                choiceContext,
                this,
                causedByEthereal: false,
                skipVisuals: false
            );
            return;
        }

        // 取消或失败：原额返还原生管线已经支付的资源，并把本牌放回手牌。
        //
        // 这里直接写回玩家战斗资源，而不走 PlayerCmd.GainEnergy / GainStars：
        // 退款不是"获得能量/星星"，走命令会误触发"每当你获得能量"一类的遗物或能力。
        _derivationReturnedToHand = true;

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

        if (Pile?.Type != PileType.Hand)
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                this,
                PileType.Hand,
                skipVisuals: false
            );
        }
    }
}
