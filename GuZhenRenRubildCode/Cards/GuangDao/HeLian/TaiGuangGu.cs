using GuZhenRenRubild.Cards.Core.Companions;
using GuZhenRenRubild.Cards.Core.Companions.Enhancements;
using GuZhenRenRubild.Cards.Core.GuangDao;
using GuZhenRenRubild.Cards.Core.Recipes;
using GuZhenRenRubild.Cards.Gu.GuangDao.Enhancements;
using GuZhenRenRubild.Characters;
using GuZhenRenRubild.Cards.Core.Catalog;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRenRubild.Cards.Gu.GuangDao.HeLian;

/// <summary>
/// 太光蛊（五转 · 光道主线，聚光爆发终端）。
///
/// <para>
/// 合练：炫光蛊 + 借光蛊（两张材料均需至少 4 转），结果转数 = 5。
/// </para>
///
/// <para>
/// B 形态纯增幅：<b>不自己创造重复折光</b>，专门奖励玩家先准备聚光、
/// 再把重复机会砸在高质量折光上——催动给 <see cref="TaiGuangGuan"/> 挂 1 次【太照】，
/// 使它下一次因聚光重复时，重复出来的第二遍折光伤害额外 +4。
/// </para>
/// </summary>
[RegisterCard(typeof(GuZhenRenRubildGuCardPool))]
[HeLianRecipe(
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.XuanGuangGu),
    typeof(global::GuZhenRenRubild.Cards.Gu.GuangDao.JieGuangGu),
    MinimumMaterialRank = 4
)]
public sealed class TaiGuangGu
    : AbstractGuangDaoHeLianGuCard,
      ICompanionEnhancementSourceGuCard
{
    /// <summary>五转单点窗口。</summary>
    public override int MaxGuRank => 5;

    /// <summary>爆发终端催动需要 2 点元气。</summary>
    public override int YuanQiCost => 2;

    /// <summary>爆发终端冷却 3 回合。</summary>
    public override int RecoveryDelayTurns => 3;

    public CompanionDefinition Companion => new(typeof(TaiGuangGuan));

    public IReadOnlyList<CompanionEnhancementGrant>
        BuildCompanionEnhancementGrants() =>
        [
            CompanionEnhancementGrant.Of<TaiZhaoEnhancement>(
                amount: 1,
                magnitude: 4,
                usesPerCombat: 1
            ),
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ModAssetPathResolver.ResolveCardPortrait(GetType().Name, ModAssetPathResolver.DefendTemplate)
    );

    public TaiGuangGu()
        : base(CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }
}
