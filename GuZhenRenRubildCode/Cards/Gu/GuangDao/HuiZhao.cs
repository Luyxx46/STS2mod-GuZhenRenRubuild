using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Runtime;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao;

/// <summary>
/// 回照（回光蛊的伴生技能牌）。
///
/// <para>
/// 基础 3 点格挡；折光是<b>纯功能</b>段：把本回合最近打出的一张攻击伴生牌从弃牌堆收回手牌。
/// 被【回照强化】强化时，被收回的牌本回合减费。
/// </para>
///
/// <para>
/// 用法：攻技攻连段的接续口——先用攻击伴生触发折光，再用回照把它捡回来。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildCardPool))]
public sealed class HuiZhao : AbstractGuangDaoCompanionCard
{
    public override Type SourceGuType => typeof(HuiGuangBackGu);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    protected override GuangDaoRefractionKind RefractionKind =>
        GuangDaoRefractionKind.Functional;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        BlockVars(3m, 0m);

    public HuiZhao()
        : base(1, CardType.Skill, TargetType.Self)
    {
    }

    protected override Task PlayBaseAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => GuCardPlay.GainBlockAsync(this, cardPlay);

    protected override async Task PlayRefractionSegmentAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        int passIndex
    )
    {
        // 弃牌堆的排列顺序就是打出的先后，因此「最后一张攻击伴生牌」
        // 正是本回合最近打出、且已经结算完的那一张。
        CardModel? recovered = PileType.Discard
            .GetPile(Owner)
            .Cards.LastOrDefault(static card =>
                card is ICompanionCard && card.Type == CardType.Attack);

        if (recovered == null)
        {
            return;
        }

        await CardPileCmd.Add(recovered, PileType.Hand);
        GuangDaoCardPlay.ApplyRecoverDiscount(recovered, this);
    }

    protected override void OnUpgrade()
    {
        UpgradeBlockVars();
    }
}
