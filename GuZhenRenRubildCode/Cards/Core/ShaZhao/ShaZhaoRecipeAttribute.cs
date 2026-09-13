using GuZhenRenRubild.Cards.Core.Abstractions;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 声明一条杀招配方。
///
/// 规则与合练配方一致：
/// 1. 材料只能是蛊牌，且类型与数量必须完全匹配；
/// 2. 玩家选择材料的先后顺序不影响匹配；
/// 3. 同一杀招可以声明多条配方；
/// 4. 两条不同杀招不能声明完全相同的材料多重集。
///
/// <code>
/// [ShaZhaoRecipe(typeof(A), typeof(A))]
/// [ShaZhaoRecipe(typeof(B), typeof(C), MinimumMaterialRank = 3)]
/// </code>
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class ShaZhaoRecipeAttribute : Attribute
{
    public ShaZhaoRecipeAttribute(params Type[] materialCardTypes)
    {
        ArgumentNullException.ThrowIfNull(materialCardTypes);

        if (materialCardTypes.Length == 0)
        {
            throw new ArgumentException(
                "一条杀招配方至少需要一张材料牌。",
                nameof(materialCardTypes)
            );
        }

        foreach (Type materialType in materialCardTypes)
        {
            ArgumentNullException.ThrowIfNull(materialType);

            if (!typeof(CardModel).IsAssignableFrom(materialType))
            {
                throw new ArgumentException(
                    $"{materialType.FullName} 不是卡牌类型。",
                    nameof(materialCardTypes)
                );
            }

            if (!typeof(IGuCard).IsAssignableFrom(materialType))
            {
                throw new ArgumentException(
                    $"{materialType.FullName} 不是蛊牌类型。",
                    nameof(materialCardTypes)
                );
            }
        }

        // 克隆保证特性参数不会被外部修改；注册表负责规范化顺序。
        MaterialCardTypes = Array.AsReadOnly(
            (Type[])materialCardTypes.Clone()
        );
    }

    /// <summary>
    /// 配方要求的材料类型。重复类型代表需要多张同名材料。
    /// </summary>
    public IReadOnlyList<Type> MaterialCardTypes { get; }

    /// <summary>
    /// 参与该配方的每张材料蛊最低转数。默认一转。
    /// 与合练不同，杀招不会因为材料转数高而获得额外收益，
    /// 因此这里只用来卡住"太弱的材料不配推演高级杀招"。
    /// </summary>
    public int MinimumMaterialRank { get; set; } = AbstractGuCard.MinimumGuRank;
}
