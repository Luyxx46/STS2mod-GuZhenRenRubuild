using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using GuZhenRenRubild.Cards.Core.Abstractions;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRenRubild.Cards.Core.ShaZhao;

/// <summary>
/// 无序杀招配方注册表。
///
/// 配方由结果牌上的 <see cref="ShaZhaoRecipeAttribute"/> 声明，首次查询时
/// 扫描杀招专属卡池一次并缓存为"配方册"。匹配时只比较材料卡类型及其数量，
/// 玩家选择材料的先后顺序不会影响结果。
///
/// 配方册在构建阶段一次性完成三件事，使后续查询不再线性扫描：
/// 1. 按材料多重集建立唯一索引（无序匹配的唯一入口）；
/// 2. 按结果牌类型分组，供"先选结果、再选材料"的界面使用；
/// 3. 汇总每种材料类型的最低转数要求，供材料筛选使用。
///
/// 新增杀招时只需要：继承 <see cref="AbstractShaZhaoCard"/>、
/// 用 <c>[RegisterCard(typeof(GuZhenRenRubildShaZhaoCardPool))]</c> 注册进杀招池，
/// 再声明 <see cref="ShaZhaoRecipeAttribute"/>。本类不需要任何改动。
/// </summary>
public static class ShaZhaoRecipeRegistry
{
    private static readonly Lazy<RecipeBook> Book = new(
        DiscoverRecipes,
        isThreadSafe: true
    );

    /// <summary>
    /// 杀招池中是否已经存在至少一条可用配方。
    /// 空池时推演入口不应被发放，避免玩家拿到一张必然失败的牌。
    /// </summary>
    public static bool HasAnyRecipe => Book.Value.All.Count > 0;

    /// <summary>
    /// 根据玩家先选定的结果牌，再匹配材料并在战斗中创建结果牌。
    /// </summary>
    public static bool TryCreateResultForTarget(
        IEnumerable<CardModel> selectedCards,
        Player owner,
        Type? targetCardType,
        [NotNullWhen(true)] out AbstractShaZhaoCard? result
    )
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        ArgumentNullException.ThrowIfNull(owner);

        CardModel[] materials = selectedCards.ToArray();

        if (materials.Length == 0 ||
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

        /*
         * 战斗中生成的卡牌必须先由当前 CombatState 创建。
         *
         * 只创建可变副本并手动设置 Owner 虽然能进入手牌，但不会登记进
         * CombatState.AllCards；之后打出时，牌堆命令会因为
         * CombatState.ContainsCard(card) 为 false 而抛出异常。
         *
         * CombatState.CreateCard 会创建可变实例、设置 Owner、登记战斗状态，
         * 并执行 AfterCreated 生命周期。
         */
        if (owner.Creature.CombatState is not { } combatState)
        {
            result = null;
            return false;
        }

        CardModel canonical = ShaZhaoCardCatalog.FindCanonical(
            recipe.ResultCardType
        );

        AbstractShaZhaoCard created = (AbstractShaZhaoCard)
            combatState.CreateCard(canonical, owner);

        try
        {
            created.InitializeFromMaterials(materials);
            result = created;
            return true;
        }
        catch
        {
            // CreateCard 已把实例登记进战斗状态；初始化失败时必须清理，
            // 避免留下不可见的悬空卡牌。
            combatState.RemoveCard(created);
            throw;
        }
    }

    /// <summary>
    /// 只检查材料是否匹配某条杀招配方，不创建战斗卡牌。
    /// 推演流程用它在创建结果前验证元气费用，避免支付失败时留下
    /// 已登记但不可见的战斗卡牌实例。
    /// </summary>
    public static bool HasMatchingRecipe(IEnumerable<CardModel> selectedCards)
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        return Book.Value.TryFindRecipe(selectedCards.ToArray(), null, out _);
    }

    /// <summary>
    /// 只检查材料是否匹配指定杀招。
    /// </summary>
    public static bool HasMatchingRecipe(
        IEnumerable<CardModel> selectedCards,
        Type targetCardType
    )
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        ArgumentNullException.ThrowIfNull(targetCardType);
        return Book.Value.TryFindRecipe(
            selectedCards.ToArray(),
            targetCardType,
            out _
        );
    }

    /// <summary>
    /// 返回当前可用材料足以完成的杀招结果类型，顺序按完整类型名稳定排序。
    /// </summary>
    public static IReadOnlyList<Type> GetCraftableResultTypes(
        IEnumerable<CardModel> availableMaterials
    )
    {
        ArgumentNullException.ThrowIfNull(availableMaterials);

        CardModel[] available = availableMaterials.ToArray();

        return Book.Value.All
            .Where(recipe => ContainsRequiredMaterials(available, recipe))
            .Select(static recipe => recipe.ResultCardType)
            .Distinct()
            .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 获取指定杀招的全部候选材料类型并集与最低转数。
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
    /// 获取指定杀招的材料数量范围。多条配方时取最小与最大张数，
    /// 最终仍由完整材料多重集校验。
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
    /// 判断材料是否至少能参与指定杀招的一条配方，包含该配方的最低转数要求。
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
    /// 获取全部配方及其材料最低转数要求，供配方大全一类的界面展示完整条件。
    /// </summary>
    public static IReadOnlyList<(
        Type ResultCardType,
        IReadOnlyList<Type> MaterialCardTypes,
        int MinimumMaterialRank
    )> GetRecipeDetails()
    {
        return Book.Value.All
            .Select(static recipe => (
                recipe.ResultCardType,
                recipe.MaterialCardTypes,
                recipe.MinimumMaterialRank
            ))
            .ToArray();
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
    /// 扫描杀招专属卡池，收集所有声明了 <see cref="ShaZhaoRecipeAttribute"/>
    /// 的非抽象杀招类型并构建配方册。顺序按完整类型名稳定排序，
    /// 保证重复配方的报错信息与配方列表顺序可复现。
    /// </summary>
    private static RecipeBook DiscoverRecipes()
    {
        List<Recipe> recipes = [];

        foreach (CardModel canonical in ShaZhaoCardCatalog.AllCards.OrderBy(
            static card => card.GetType().FullName ?? card.GetType().Name,
            StringComparer.Ordinal
        ))
        {
            Type resultType = canonical.GetType();

            if (resultType.IsAbstract ||
                !typeof(AbstractShaZhaoCard).IsAssignableFrom(resultType))
            {
                continue;
            }

            foreach (
                ShaZhaoRecipeAttribute attribute in resultType
                    .GetCustomAttributes<ShaZhaoRecipeAttribute>(inherit: false)
            )
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

        if (recipes.Count > 0)
        {
            Entry.Logger.Info(
                $"[杀招] 已登记 {recipes.Count} 条杀招配方，涉及 " +
                $"{recipes.Select(static recipe => recipe.ResultCardType).Distinct().Count()} 张杀招牌。"
            );
        }
        else
        {
            Entry.Logger.Info(
                "[杀招] 杀招池为空：当前没有任何杀招配方，" +
                "推演入口不会被发放。加入带 [ShaZhaoRecipe] 的杀招牌即可启用。"
            );
        }

        return RecipeBook.Build(recipes);
    }

    /// <summary>
    /// 一条无序杀招配方。材料多重集在构造时压成排序后的类型名键，
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
                        "Duplicate unordered ShaZhao recipe: " +
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
