using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.XianYuan;

/// <summary>
/// 青提仙元：六转空窍对应的最小面额仙元牌，提供 2 个催动单位。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildXianYuanCardPool))]
public sealed class QingTiXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 2;

    public override int GrantedApertureRank => 6;
}
