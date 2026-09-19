using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Basic.YuPiGu;

/// <summary>
/// 玉皮蛊的永久伴生普通牌「玉皮甲」。
///
/// <para>
/// 它是本模组目前唯一的<b>桥接牌</b>：不带光道标签，因此没有折光段，
/// 但照常把自己的类型（技能）提交进折光历史，帮助下一张攻击牌折光。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YuPiCompanion : AbstractCompanionCard
{
    // 目录名与来源蛊类名同为 YuPiGu，命名空间因此与类型同名，必须用 global:: 全限定名引用。
    public override Type SourceGuType =>
        typeof(global::GuZhenRenRubild.Cards.Basic.YuPiGu.YuPiGu);

    // 专属卡图按类名解析：images/cards/YuPiCompanion.png 存在就用，缺失时回退模板防御图。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6m, ValueProp.Move)];

    // 告诉游戏该卡会获得格挡，供提示、统计及其他规则系统识别。
    public override bool GainsBlock => true;

    public YuPiCompanion()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    // 伴生牌属于普通卡牌体系，直接复用蛊牌共用的格挡出牌动作。
    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.GainBlockAsync(this, cardPlay);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
