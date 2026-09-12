using MegaCrit.Sts2.Core.Entities.Cards;

namespace GuZhenRenRubild.Cards.Core.Recipes;

/// <summary>
/// 所有专属合练蛊的公共父类。
///
/// 公共规则：
/// 1. 具体合练蛊各自声明 [RegisterCard] 进入蛊牌主奖励池（与本模组其他蛊牌一致）；
/// 2. 是否进入普通卡牌奖励由具体卡牌显式实现 <see cref="IHeLianCardRewardEligible"/> 决定；
/// 3. 合练生成时默认取材料最高转数，具体结果牌可重写计算策略；
/// 4. 费用固定为 2 点元气，且不允许被战斗内随机生成。
/// </summary>
public abstract class AbstractHeLianGuCard : AbstractGuCard
{
    public override int YuanQiCost => 2;

    protected AbstractHeLianGuCard(
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    )
        : base(type, rarity, target, showInCardLibrary)
    {
        // 这里刻意不写转数：未参与合练时（图鉴、预览）基类 getter 本身就返回
        // MinimumGuRank，而写入会在这个共享的 canonical 实例上留下持久化状态。
        // 转数只在合练结算时由 InitializeFromHeLian 写入。
    }

    public override bool CanBeGeneratedInCombat => false;
}
