using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using GuZhenRenRubild.Cards;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Combat;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Relics;

// RegisterRelic 将遗物注册进角色专属遗物池。
// RegisterCharacterStarterRelic 将它设为该角色的初始遗物；蛊牌的打出后归位和元气回合恢复都由此遗物挂接原生时机。
[RegisterRelic(typeof(GuZhenRenRubildRelicPool))]
[RegisterCharacterStarterRelic(typeof(GuZhenRenRubildCharacter))]
public sealed class GuZhenRenRubildRelic : ModRelicTemplate
{
    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Common;

    // 图片资源统一放在 AssetProfile 里配置。
    // 三个路径可以先指向同一张图。后续有高清图或轮廓图时再拆开。
    public override RelicAssetProfile AssetProfile => new(
        // 小图标（原版 85x85）。
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        // 轮廓图标（原版 85x85）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        // 大图标（原版 256x256）。
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 蛊牌正式结算前处理激活登记。只响应本遗物持有者的蛊牌，并且一组连锁打出只处理第一张，避免重复计数。
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        await base.BeforeCardPlayed(cardPlay);

        if (!ReferenceEquals(cardPlay.Card.Owner, Owner) ||
            cardPlay.Card is not IGuCard ||
            !cardPlay.IsFirstInSeries)
        {
            return;
        }

        // 本地玩家从额外手牌点击蛊牌时，RitsuLib 会先把卡移动到原生手牌；远端客户端重放同一动作时没有这一步界面事务。
        // 因此这里在原生打牌校验继续之前补做一次幂等移动：只有卡仍位于激活区时才移动，确保本地与联机端状态一致。
        if (!cardPlay.IsAutoPlay &&
            cardPlay.Card.Pile?.Type == GuCardPileSystem.ActivePileType)
        {
            GuCardPileSystem.MoveToNativeHand(cardPlay.Card);
        }

        // 进入实际结算前消耗一次当前激活周期的使用次数。
        GuCardRuntime.RegisterActivation(cardPlay.Card);
    }

    // 蛊牌结算完成后，根据剩余使用次数送回激活区或恢复区，然后尝试从储备区补满激活区。
    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);

        if (!ReferenceEquals(cardPlay.Card.Owner, Owner) ||
            cardPlay.Card is not IGuCard ||
            !cardPlay.IsFirstInSeries)
        {
            return;
        }

        // GetResultPile 在次数耗尽时还会首次写入恢复完成回合。
        PileType resultPile = GuCardRuntime.GetResultPile(cardPlay.Card);
        await GuCardPileSystem.MoveCardToPileAsync(
            cardPlay.Card,
            resultPile,
            skipVisuals: false
        );
        await GuCardPileSystem.RefillActiveAsync(Owner, skipVisuals: false);
    }

    // 原生能量重置后执行每回合蛊牌维护：先恢复到期蛊牌，再补充元气。
    public override async Task AfterEnergyReset(Player player)
    {
        await base.AfterEnergyReset(player);

        if (!ReferenceEquals(player, Owner) || player.PlayerCombatState == null)
        {
            return;
        }

        // 先处理恢复区，确保本回合到期的蛊牌能在元气刷新后立即正常使用。
        int turn = player.PlayerCombatState.TurnNumber;
        await GuCardPileSystem.RestoreRecoveredCardsAsync(player, turn);

        int current = SecondaryResourceCmd.Get(player, YuanQiSystem.ResourceId);
        int maximum = SecondaryResourceCmd.GetMax(player, YuanQiSystem.ResourceId)
            ?? YuanQiSystem.Definition.HardMaxAmount;
        // 第 1 回合直接补满元气；之后每回合恢复 2 点，但绝不超过当前资源上限。
        int target = turn <= 1 ? maximum : Math.Min(maximum, current + 2);

        if (target != current)
        {
            await SecondaryResourceCmd.Set(
                player,
                YuanQiSystem.ResourceId,
                target,
                source: this
            );
        }
    }
}
