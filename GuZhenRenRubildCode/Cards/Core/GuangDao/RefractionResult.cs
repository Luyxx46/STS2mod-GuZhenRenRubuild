namespace GuZhenRenRubild.Cards.Core.GuangDao;

/// <summary>
/// 一次正式出牌序列的折光结果。
///
/// <para>
/// <see cref="Triggered"/> 只表示<b>真实折光</b>（自然交替或强制折光成立）；
/// <see cref="EffectResolutionCount"/> 表示该牌自身的折光段应当结算几次
/// ——1 表示正常结算，2 表示聚光命中、整段额外再结算一次。
/// </para>
/// </summary>
public readonly record struct RefractionResult(
    bool Triggered,
    int EffectResolutionCount
)
{
    /// <summary>未触发折光。</summary>
    public static RefractionResult None => new(false, 0);
}
