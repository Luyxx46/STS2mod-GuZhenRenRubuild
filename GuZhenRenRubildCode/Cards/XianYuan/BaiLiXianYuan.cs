using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.XianYuan;

/// <summary>
/// 白荔仙元：八转空窍对应的仙元牌，提供 8 个催动单位（等于两张红枣仙元）。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildXianYuanCardPool))]
public sealed class BaiLiXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 8;

    public override int GrantedApertureRank => 8;
}
