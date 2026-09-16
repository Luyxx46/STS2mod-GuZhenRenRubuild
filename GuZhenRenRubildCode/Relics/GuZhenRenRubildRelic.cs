using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using GuZhenRenRubild.Aperture;
using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Cards.Core.ImmortalEssence;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Cards.Core.ShaZhao;
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
[RegisterCharacterStarterRelic(typeof(GuYueFangYuan))]
public sealed class GuZhenRenRubildRelic
    : ModRelicTemplate, ISecondaryResourceHookListener
{
    // 记录已经画到界面上的转数，只有转数变化时才请求重载图标。
    private int _lastVisualRank = -1;

    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Common;

    // 遗物角标显示当前空窍转数。
    public override bool ShowCounter => IsMutable;

    // 遗物角标与图标都走同一处转数读取，避免两条路径各写一遍守卫与兜底。
    public override int DisplayAmount => GetApertureRank();

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

        // 六转及以上的蛊牌催动时额外扣减仙元单位（元气照付）。
        // 可打出判定本应已拦住余额不足的情况；万一漏到这里只记日志，不打断打出流程。
        if (cardPlay.Card is IGuCard playedGu)
        {
            int essenceCost = ImmortalEssenceSystem.GetActivationCost(
                playedGu.GuRank
            );

            if (essenceCost > 0 &&
                !ImmortalEssenceSystem.TrySpend(Owner, essenceCost))
            {
                Entry.Logger.Warn(
                    $"[仙元] {cardPlay.Card.Id} 催动时应扣 {essenceCost} 个仙元单位，" +
                    "但扣减失败（可打出判定应已拦截）。"
                );
            }
        }
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

    // 战斗开始事务：重置本场战斗的杀招推演进度，并清掉上一场战斗可能残留的材料绑定。
    // 重连或房间重建会重复触发，因此重置内部按运行层数去重。
    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        ApertureSystem.HandleCombatStarting(Owner);

        // 战斗卡编号会在新战斗里重新分配，因此残留绑定必须在新战斗开始前清掉，
        // 否则旧编号可能恰好被新战斗中的另一张牌占用。
        ShaZhaoBindingService.ClearStaleBindings(Owner);
    }

    // 本场第一次初始抽牌前发放系统牌：先发"杀招推演"（三转起），再发"仙元"（六转起）。
    // 两者都不占起手抽牌，且各自按运行层数去重（重连安全）。
    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState
    )
    {
        if (!ReferenceEquals(player, Owner))
        {
            return;
        }

        await ApertureSystem.HandleShaZhaoDerivationGrantAsync(player);
        await ApertureSystem.HandleXianYuanGrantAsync(player);
    }

    // 战斗结束兜底：清理全部杀招材料绑定。
    // 材料留在原地，由游戏的战斗牌堆回收流程送回牌组；这里只解除绑定记录，
    // 避免绑定状态跨战斗残留导致材料在下一场战斗中被判定为已被封装。
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await base.AfterCombatEnd(room);
        ShaZhaoBindingService.FinalizeAllForCombatEnd(Owner);
    }

    // 起点遗物作为整个 Run 中稳定存在的 Hook listener，负责把独立"升炼"与"合练"选项加入篝火。
    // 两个选项各自按 OptionId 幂等注入；它们遵循游戏原生规则——每个休息点只能执行一次行动。
    public override bool TryModifyRestSiteOptions(
        Player player,
        ICollection<RestSiteOption> options
    )
    {
        bool modified = base.TryModifyRestSiteOptions(player, options);
        if (!ReferenceEquals(player, Owner))
        {
            return modified;
        }

        if (!options.Any(option => option.OptionId == GuRankUpRestSiteOption.OptionIdentifier))
        {
            options.Add(new GuRankUpRestSiteOption(player));
            modified = true;
        }

        if (!options.Any(option => option.OptionId == GuHeLianRestSiteOption.OptionIdentifier))
        {
            options.Add(new GuHeLianRestSiteOption(player));
            modified = true;
        }

        return modified;
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
    /// 元气上限随空窍转数变化：一至九转依次为 3、4、4、5、5、6、6、6、6
    /// （六转进入仙窍后不再提升）。
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
    // ApertureSystem.GetState 每次都会把转数规范化到
    // [MinimumRank, MaximumImplementedRank]，因此对调用方而言这里的 clamp
    // 只是防御性兜底，不改变读取到的数值（角标显示与图标路径共用本方法）。
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
            // 遗物尚未挂到玩家身上时读取会失败，退回初始转数。
            return ApertureProgression.MinimumRank;
        }
    }
}
