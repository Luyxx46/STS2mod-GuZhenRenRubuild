using GuZhenRenRubild.Cards.Core;
using GuZhenRenRubild.Cards.Gu;
using GuZhenRenRubild.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Core.Companions;

/// <summary>玉皮蛊的永久伴生普通牌。</summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class YuPiCompanion : AbstractCompanionCard
{
    public override Type SourceGuType => typeof(YuPiGu);

    // 暂时复用模板防御牌卡图，后续可替换为玉皮甲专属资源。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/GuZhenRenRubildDefend.png"
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
