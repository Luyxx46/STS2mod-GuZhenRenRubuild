using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.XianYuan;

/// <summary>
/// 红枣仙元：七转空窍对应的仙元牌，提供 4 个催动单位（等于两张青提仙元）。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildXianYuanCardPool))]
public sealed class HongZaoXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 4;

    public override int GrantedApertureRank => 7;
}
