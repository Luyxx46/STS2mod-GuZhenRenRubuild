using GuZhenRenRubild.Cards.Core.Abstractions;
using GuZhenRenRubild.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRenRubild.Cards.XianYuan;

/// <summary>
/// 黄杏仙元：九转空窍对应的最大面额仙元牌，提供 16 个催动单位（等于两张白荔仙元）。
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildXianYuanCardPool))]
public sealed class HuangXingXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 16;

    public override int GrantedApertureRank => 9;
}
