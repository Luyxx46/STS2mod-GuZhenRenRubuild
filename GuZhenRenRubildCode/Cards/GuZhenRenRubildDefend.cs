using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using GuZhenRenRubild.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards;

// 防御牌和打击一样注册到角色卡池，并作为 4 张初始卡加入角色卡组。
// 这是一张模板用的普通防御牌，主要展示角色卡池注册、动态格挡值和升级写法。
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenRubildCharacter), 4)]
public sealed class GuZhenRenRubildDefend : ModCardTemplate
{
    // 基础耗能。
    private const int BaseEnergyCost = 1;
    // 卡牌类型。
    private const CardType CardKind = CardType.Skill;
    // 卡牌稀有度。
    private const CardRarity CardRarityValue = CardRarity.Basic;
    // 目标类型（Self 表示自己）。
    private const TargetType CardTarget = TargetType.Self;
    // 是否在卡牌图鉴中显示。
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    // 卡图资源。
    // 如果你按这行代码写，文件名就对应 GuZhenRenRubild/images/cards/GuZhenRenRubildDefend.png。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

    // 卡牌基础数值。
    // BlockVar 会绑定到本地化里的 {Block:diff()}，升级时文本会自动显示差值。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    // 构造函数只负责把静态卡牌元数据交给基类；具体效果数值由 CanonicalVars 提供。
    public GuZhenRenRubildDefend() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑，这里是获得格挡。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    // 升级后的效果逻辑。
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
