using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.Recipes;

/// <summary>
/// 无序合练配方注册表。
///
/// 配方由结果牌上的 <see cref="HeLianRecipeAttribute"/> 声明，首次查询时
/// 从蛊牌主奖励池扫描一次并缓存为"配方册"。匹配时只比较材料卡类型及其数量，
/// 玩家选择材料的先后顺序不会影响结果。
///
/// 配方册在构建阶段一次性完成三件事，使后续查询不再线性扫描：
/// 1. 按材料多重集建立唯一索引（无序匹配的唯一入口）；
/// 2. 按结果牌类型分组，供"先选结果、再选材料"的界面使用；
/// 3. 汇总每种材料类型的最低转数要求，供材料筛选使用。
/// </summary>
public static class HeLianRecipeRegistry
{
    private static readonly Lazy<RecipeBook> Book = new(
        DiscoverRecipes,
        isThreadSafe: true
    );

    /// <summary>
    /// 根据玩家先选定的结果牌，再匹配材料并创建结果牌。
    /// </summary>
    public static bool TryCreateResultForTarget(
        IEnumerable<CardModel> selectedCards,
        Player owner,
        Type? targetCardType,
        [NotNullWhen(true)] out AbstractGuCard? result
    )
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        ArgumentNullException.ThrowIfNull(owner);

        CardModel[] materials = selectedCards.ToArray();

        if (materials.Length < 2 ||
            materials.Any(static card => card is not IGuCard))
        {
            result = null;
            return false;
        }

        // 每条配方都有各自的最低转数要求，材料必须逐张达标。
        if (!Book.Value.TryFindRecipe(
                materials,
                targetCardType,
                out Recipe? recipe
            ))
        {
            result = null;
            return false;
        }

        if (materials.Any(card =>
                card is not IGuCard gu ||
                gu.GuRank < recipe.MinimumMaterialRank
            ))
        {
            result = null;
            return false;
        }

        CardModel canonical = GuCardCatalog.FindCanonical(
            recipe.ResultCardType
        );

        // 牌组中的卡牌必须先由当前 RunState 创建并登记。
        // 仅调用 canonical.ToMutable() 再设置 Owner 不会把实例加入
        // RunState，随后 CardPileCmd.Add(..., PileType.Deck) 会抛出：
        // "must be added to a RunState before adding it to your deck"。
        AbstractGuCard createdResult = (AbstractGuCard)owner.RunState.CreateCard(
            canonical,
            owner
        );

        try
        {
            createdResult.InitializeFromHeLian(materials);
            result = createdResult;
            return true;
        }
        catch
        {
            // CreateCard 已把结果实例加入运行状态；初始化失败时必须
            // 清理该未完成实例，避免留下不可见的悬空卡牌。
            owner.RunState.RemoveCard(createdResult);
            throw;
        }
    }

    /// <summary>
    /// 根据当前可用材料，返回至少一条可制作配方所需的选牌范围。
    /// 配方本身决定材料数，因此这里没有固定"两张牌"限制。
    /// </summary>
    public static bool TryGetCraftableMaterialCountRange(
        IEnumerable<CardModel> availableMaterials,
        out int minimum,
        out int maximum
    )
    {
        ArgumentNullException.ThrowIfNull(availableMaterials);

        int[] craftableCounts = GetCraftableRecipes(
                availableMaterials.ToArray()
            )
            .Select(static recipe => recipe.MaterialCardTypes.Count)
            .ToArray();

        if (craftableCounts.Length == 0)
        {
            minimum = 0;
            maximum = 0;
            return false;
        }

        minimum = craftableCounts.Min();
        maximum = craftableCounts.Max();
        return true;
    }

    /// <summary>
    /// 返回当前材料足以完成的合练结果牌类型，顺序按完整类型名稳定排序。
    /// </summary>
    public static IReadOnlyList<Type> GetCraftableResultTypes(
        IEnumerable<CardModel> availableMaterials
    )
    {
        ArgumentNullException.ThrowIfNull(availableMaterials);

        return GetCraftableRecipes(availableMaterials.ToArray())
            .Select(static recipe => recipe.ResultCardType)
            .Distinct()
            .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 获取指定结果牌的全部候选材料类型并集与最低转数。
    /// 结果牌可声明多条配方，玩家选定结果后仍可在这些配方之间自由选择。
    /// </summary>
    public static IReadOnlyList<Type> GetMaterialTypesForResult(
        Type resultCardType,
        out int minimumMaterialRank
    )
    {
        ArgumentNullException.ThrowIfNull(resultCardType);

        IReadOnlyList<Recipe> recipes = Book.Value.GetRecipesForResult(
            resultCardType
        );

        minimumMaterialRank = recipes.Count == 0
            ? 0
            : recipes.Min(static recipe => recipe.MinimumMaterialRank);

        return recipes
            .SelectMany(static recipe => recipe.MaterialCardTypes)
            .Distinct()
            .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 判断材料是否至少能参与指定结果牌的一条配方，包含该配方的最低转数要求。
    /// </summary>
    public static bool IsEligibleMaterialCardForResult(
        CardModel card,
        Type resultCardType
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(resultCardType);

        return card is IGuCard gu &&
            Book.Value.GetRecipesForResult(resultCardType).Any(recipe =>
                recipe.MaterialCounts.ContainsKey(card.GetType()) &&
                gu.GuRank >= recipe.MinimumMaterialRank
            );
    }

    /// <summary>
    /// 获取指定合练结果牌的材料数量范围。
    /// </summary>
    public static bool GetMaterialCountRangeForResult(
        Type resultCardType,
        out int minimum,
        out int maximum
    )
    {
        ArgumentNullException.ThrowIfNull(resultCardType);

        int[] counts = Book.Value
            .GetRecipesForResult(resultCardType)
            .Select(static recipe => recipe.MaterialCardTypes.Count)
            .ToArray();

        if (counts.Length == 0)
        {
            minimum = 0;
            maximum = 0;
            return false;
        }

        minimum = counts.Min();
        maximum = counts.Max();
        return true;
    }

    /// <summary>
    /// 判断具体卡牌是否满足至少一条配方的材料类型与最低转数要求。
    /// </summary>
    public static bool IsEligibleMaterialCard(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuCard gu &&
            Book.Value.TryGetMinimumMaterialRank(
                card.GetType(),
                out int minimumMaterialRank
            ) &&
            gu.GuRank >= minimumMaterialRank;
    }

    /// <summary>
    /// 获取全部配方及其材料最低转数要求。
    /// 供后续的配方大全界面展示完整条件。
    /// </summary>
    public static IReadOnlyList<(
        Type ResultCardType,
        IReadOnlyList<Type> MaterialCardTypes,
        int MinimumMaterialRank
    )> GetRecipeDetails()
    {
        return Book.Value
            .All
            .Select(static recipe => (
                recipe.ResultCardType,
                recipe.MaterialCardTypes,
                recipe.MinimumMaterialRank
            ))
            .ToArray();
    }

    /// <summary>
    /// 扫描蛊牌主奖励池，收集所有声明了 <see cref="HeLianRecipeAttribute"/>
    /// 的非抽象蛊牌类型并构建配方册。顺序按完整类型名稳定排序，
    /// 保证重复配方的报错信息与配方列表顺序可复现。
    /// </summary>
    private static RecipeBook DiscoverRecipes()
    {
        List<Recipe> recipes = [];

        foreach (CardModel canonical in GuCardCatalog.AllCards.OrderBy(
            static card => card.GetType().FullName ?? card.GetType().Name,
            StringComparer.Ordinal
        ))
        {
            Type resultType = canonical.GetType();

            if (resultType.IsAbstract ||
                !typeof(AbstractGuCard).IsAssignableFrom(resultType))
            {
                continue;
            }

            foreach (HeLianRecipeAttribute attribute in resultType
                .GetCustomAttributes<HeLianRecipeAttribute>(inherit: false))
            {
                recipes.Add(
                    new Recipe(
                        resultType,
                        Array.AsReadOnly(attribute.MaterialCardTypes.ToArray()),
                        Math.Max(
                            AbstractGuCard.MinimumGuRank,
                            attribute.MinimumMaterialRank
                        )
                    )
                );
            }
        }

        return RecipeBook.Build(recipes);
    }

    private static IEnumerable<Recipe> GetCraftableRecipes(
        CardModel[] availableMaterials
    )
    {
        return Book.Value.All.Where(recipe =>
            ContainsRequiredMaterials(availableMaterials, recipe)
        );
    }

    private static bool ContainsRequiredMaterials(
        IReadOnlyList<CardModel> available,
        Recipe recipe
    )
    {
        Dictionary<Type, int> availableCounts = CountTypes(
            available
                .Where(card =>
                    card is IGuCard gu &&
                    gu.GuRank >= recipe.MinimumMaterialRank
                )
                .Select(static card => card.GetType())
        );

        foreach ((Type type, int requiredCount) in recipe.MaterialCounts)
        {
            if (!availableCounts.TryGetValue(type, out int availableCount) ||
                availableCount < requiredCount)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<Type, int> CountTypes(IEnumerable<Type> types)
    {
        Dictionary<Type, int> counts = [];

        foreach (Type type in types)
        {
            counts[type] = counts.TryGetValue(type, out int current)
                ? current + 1
                : 1;
        }

        return counts;
    }

    /// <summary>
    /// 一条无序合练配方。材料多重集在构造时压成排序后的类型名键，
    /// 使"材料是否完全相同"退化为一次字符串比较。
    /// </summary>
    private sealed class Recipe
    {
        internal Recipe(
            Type resultCardType,
            IReadOnlyList<Type> materialCardTypes,
            int minimumMaterialRank
        )
        {
            ResultCardType = resultCardType;
            MaterialCardTypes = materialCardTypes;
            MinimumMaterialRank = minimumMaterialRank;
            MaterialCounts = CountTypes(materialCardTypes);
            MaterialKey = BuildMaterialKey(materialCardTypes);
        }

        internal Type ResultCardType { get; }

        internal IReadOnlyList<Type> MaterialCardTypes { get; }

        internal int MinimumMaterialRank { get; }

        /// <summary>材料类型 -> 需要张数。</summary>
        internal IReadOnlyDictionary<Type, int> MaterialCounts { get; }

        /// <summary>无序材料键：把材料类型名排序后拼接，忽略玩家选择顺序。</summary>
        internal string MaterialKey { get; }

        internal static string BuildMaterialKey(
            IEnumerable<Type> materialCardTypes
        )
        {
            string[] names = materialCardTypes
                .Select(static type => type.FullName ?? type.Name)
                .ToArray();

            Array.Sort(names, StringComparer.Ordinal);
            return string.Join('|', names);
        }
    }

    /// <summary>
    /// 配方册：一次性构建的只读索引集合。
    /// </summary>
    private sealed class RecipeBook
    {
        private readonly Dictionary<string, Recipe> _byMaterials;
        private readonly Dictionary<Type, IReadOnlyList<Recipe>> _byResultType;
        private readonly Dictionary<Type, int> _minimumRankByMaterialType;

        private RecipeBook(
            IReadOnlyList<Recipe> all,
            Dictionary<string, Recipe> byMaterials,
            Dictionary<Type, IReadOnlyList<Recipe>> byResultType,
            Dictionary<Type, int> minimumRankByMaterialType
        )
        {
            All = all;
            _byMaterials = byMaterials;
            _byResultType = byResultType;
            _minimumRankByMaterialType = minimumRankByMaterialType;
        }

        internal IReadOnlyList<Recipe> All { get; }

        internal static RecipeBook Build(IReadOnlyList<Recipe> recipes)
        {
            Dictionary<string, Recipe> byMaterials =
                new(StringComparer.Ordinal);
            Dictionary<Type, List<Recipe>> byResultType = [];
            Dictionary<Type, int> minimumRankByMaterialType = [];

            foreach (Recipe recipe in recipes)
            {
                // 无序配方必须全局唯一，否则同一批材料会匹配到两个结果。
                if (byMaterials.TryGetValue(
                        recipe.MaterialKey,
                        out Recipe? duplicate
                    ))
                {
                    throw new InvalidOperationException(
                        "Duplicate unordered HeLian recipe: " +
                        $"{duplicate.ResultCardType.FullName} and " +
                        $"{recipe.ResultCardType.FullName} use the same materials."
                    );
                }

                byMaterials.Add(recipe.MaterialKey, recipe);

                if (!byResultType.TryGetValue(
                        recipe.ResultCardType,
                        out List<Recipe>? sameResult
                    ))
                {
                    sameResult = [];
                    byResultType.Add(recipe.ResultCardType, sameResult);
                }

                sameResult.Add(recipe);

                foreach (Type materialType in recipe.MaterialCounts.Keys)
                {
                    // 同一材料出现在多条配方时取最宽松的要求：
                    // 这正是"至少满足一条配方"的等价值。
                    if (!minimumRankByMaterialType.TryGetValue(
                            materialType,
                            out int current
                        ) ||
                        recipe.MinimumMaterialRank < current)
                    {
                        minimumRankByMaterialType[materialType] =
                            recipe.MinimumMaterialRank;
                    }
                }
            }

            Dictionary<Type, IReadOnlyList<Recipe>> byResultTypeView =
                byResultType.ToDictionary(
                    static pair => pair.Key,
                    static pair => (IReadOnlyList<Recipe>)pair.Value
                );

            return new RecipeBook(
                recipes,
                byMaterials,
                byResultTypeView,
                minimumRankByMaterialType
            );
        }

        internal bool TryFindRecipe(
            IReadOnlyList<CardModel> materials,
            Type? targetCardType,
            [NotNullWhen(true)] out Recipe? recipe
        )
        {
            string key = Recipe.BuildMaterialKey(
                materials.Select(static card => card.GetType())
            );

            if (!_byMaterials.TryGetValue(key, out recipe))
            {
                recipe = null;
                return false;
            }

            // 指定了目标结果时，只有该结果的配方才算命中。
            if (targetCardType != null &&
                recipe.ResultCardType != targetCardType)
            {
                recipe = null;
                return false;
            }

            return true;
        }

        internal IReadOnlyList<Recipe> GetRecipesForResult(
            Type resultCardType
        )
        {
            return _byResultType.TryGetValue(
                resultCardType,
                out IReadOnlyList<Recipe>? recipes
            )
                ? recipes
                : [];
        }

        internal bool TryGetMinimumMaterialRank(
            Type materialCardType,
            out int minimumMaterialRank
        )
        {
            return _minimumRankByMaterialType.TryGetValue(
                materialCardType,
                out minimumMaterialRank
            );
        }
    }
}
