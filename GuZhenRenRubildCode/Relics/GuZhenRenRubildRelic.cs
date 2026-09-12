using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Combat;
using GuZhenRenRubild.RestSite;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Relics;

// RegisterRelic 将遗物注册进角色专属遗物池。
// RegisterCharacterStarterRelic 将它设为该角色的初始遗物；蛊牌的打出后归位和元气回合恢复都由此遗物挂接原生时机。
// 同时它也是"空窍"本体的载体：承载 1～9 转状态、驱动元气上限与每回合恢复，并按转数切换图标。
[RegisterRelic(typeof(GuZhenRenRubildRelicPool))]
[RegisterCharacterStarterRelic(typeof(GuZhenRenRubildCharacter))]
public sealed class GuZhenRenRubildRelic
    : ModRelicTemplate, ISecondaryResourceHookListener
{
    // 记录已经画到界面上的转数，只有转数变化时才请求重载图标。
    private int _lastVisualRank = -1;

    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Common;

    // 遗物角标显示当前空窍转数。
    public override bool ShowCounter => IsMutable;

    public override int DisplayAmount
    {
        get
        {
            if (!IsMutable || !ApertureSystem.IsInitialized)
            {
                return ApertureProgression.MinimumRank;
            }

            try
            {
                return ApertureSystem.GetState(Owner).Rank;
            }
            catch
            {
                // 遗物尚未挂到玩家身上时读取会失败，退回初始转数。
                return ApertureProgression.MinimumRank;
            }
        }
    }

    // 供遗物描述直接引用：当前转数、当前修为、突破所需修为与是否已到九转。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        base.CanonicalVars.Concat(
            [
                new IntVar("Rank", ApertureProgression.MinimumRank),
                new IntVar("CurrentXp", 0),
                new IntVar(
                    "RequiredXp",
                    ApertureProgression.GetRequiredXp(
                        ApertureProgression.MinimumRank
                    )
                ),
                new IntVar("CultivationComplete", 0),
            ]
        );

    // 图片资源统一放在 AssetProfile 里配置。
    // 图标按当前转数切换为 images/relics/GuZhenRenRubildRelic<转数>.png；
    // 轮廓图不单独提供，交给模板回退。
    public override RelicAssetProfile AssetProfile
    {
        get
        {
            string iconPath =
                $"{Entry.ResPath}/images/relics/" +
                $"{GetType().Name}{GetApertureRank()}.png";

            return new RelicAssetProfile(
                IconPath: iconPath,
                BigIconPath: iconPath);
        }
    }

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

    // 战斗胜利后结算空窍修为：普通/精英/首领战分别提供不同修为，修为足够时提升转数。
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        await base.AfterCombatVictory(room);

        // 空窍运行时在遗物所属玩家身上推进转数，并补发六转起的最大生命奖励。
        await ApertureSystem.HandleCombatVictoryAsync(Owner, room);
    }

    // 原生能量重置后执行每回合蛊牌维护：先恢复到期蛊牌，再按空窍转数补充元气。
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

        int rank = GetApertureRank();
        int current = SecondaryResourceCmd.Get(player, YuanQiSystem.ResourceId);
        int maximum = SecondaryResourceCmd.GetMax(player, YuanQiSystem.ResourceId)
            ?? YuanQiSystem.Definition.HardMaxAmount;

        // 第 1 回合直接补满至当前转数上限；之后每回合按转数恢复，但不超过当前资源上限。
        int target = turn <= 1
            ? ApertureProgression.GetYuanQiStartAmount(rank)
            : current + ApertureProgression.GetYuanQiRecovery(rank);
        int clamped = Math.Clamp(
            target,
            YuanQiSystem.Definition.MinAmount,
            maximum
        );

        if (clamped != current)
        {
            await SecondaryResourceCmd.Set(
                player,
                YuanQiSystem.ResourceId,
                clamped,
                source: this
            );
        }
    }

    // 起点遗物作为整个 Run 中稳定存在的 Hook listener，负责把独立"升炼"选项加入篝火。
    public override bool TryModifyRestSiteOptions(
        Player player,
        ICollection<RestSiteOption> options
    )
    {
        bool modified = base.TryModifyRestSiteOptions(player, options);
        if (!ReferenceEquals(player, Owner) ||
            options.Any(option => option.OptionId == GuRankUpRestSiteOption.OptionIdentifier))
        {
            return modified;
        }

        options.Add(new GuRankUpRestSiteOption(player));
        return true;
    }

    // 奖励候选为 0 时不展示一个无法选择的空 CardReward。
    public override bool TryModifyRewardsLate(
        Player player,
        List<Reward> rewards,
        AbstractRoom? room
    )
    {
        bool modified = base.TryModifyRewardsLate(player, rewards, room);
        if (!ReferenceEquals(player, Owner))
        {
            return modified;
        }

        int removed = rewards.RemoveAll(reward =>
            reward is CardReward cardReward && !cardReward.IsPopulated
        );
        return modified || removed > 0;
    }

    // 获得遗物后立即把转数与图标同步到界面。
    public override Task AfterObtained()
    {
        ApertureSystem.RefreshRelicVisualState(Owner);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 元气上限随空窍转数变化：一至九转依次为 3、4、4、5、5、7、7、8、9。
    /// </summary>
    public decimal ModifyMaxSecondaryResource(
        SecondaryResourceMaxContext context,
        decimal amount
    )
    {
        if (!ReferenceEquals(context.Player, Owner) ||
            !string.Equals(
                context.Definition.Id,
                YuanQiSystem.ResourceId,
                StringComparison.Ordinal
            ))
        {
            return amount;
        }

        return ApertureProgression.GetYuanQiCapacity(GetApertureRank());
    }

    /// <summary>
    /// 由空窍运行时在转数变化或数据恢复后调用，刷新图标与描述数值。
    /// </summary>
    internal void RefreshApertureVisualState(ApertureRunData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        AssertMutable();

        Status = RelicStatus.Normal;

        if (_lastVisualRank != data.Rank)
        {
            _lastVisualRank = data.Rank;
            RelicIconChanged();
        }

        DynamicVars["Rank"].BaseValue = data.Rank;
        DynamicVars["CurrentXp"].BaseValue = data.Xp;
        DynamicVars["RequiredXp"].BaseValue =
            ApertureProgression.GetRequiredXp(data.Rank);
        DynamicVars["CultivationComplete"].BaseValue =
            data.IsCultivationComplete ? 1 : 0;

        InvokeDisplayAmountChanged();
    }

    // 读取当前转数；遗物尚未挂到玩家、或运行时未就绪时退回初始转数。
    private int GetApertureRank()
    {
        if (!IsMutable || !ApertureSystem.IsInitialized)
        {
            return ApertureProgression.MinimumRank;
        }

        try
        {
            return Math.Clamp(
                ApertureSystem.GetState(Owner).Rank,
                ApertureProgression.MinimumRank,
                ApertureProgression.MaximumImplementedRank
            );
        }
        catch
        {
            return ApertureProgression.MinimumRank;
        }
    }
}
