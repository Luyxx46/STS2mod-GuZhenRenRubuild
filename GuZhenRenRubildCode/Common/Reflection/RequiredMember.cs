using System.Reflection;

using HarmonyLib;

namespace GuZhenRenRubild.Common.Reflection;

/// <summary>
/// Harmony 补丁的反射查找助手。
///
/// 每个补丁都要先按名字拿到游戏内部的方法，缺失时说明"游戏版本与模组不匹配"。
/// 把查找与报错集中在这里，可以让补丁代码只表达"我要补哪个方法"，
/// 并且所有缺失错误的措辞、类型与成员名格式保持一致。
///
/// 需要"找不到就跳过"的场景（可选补丁）请直接使用 <see cref="AccessTools"/>，
/// 不要用本类，以免把可选依赖变成硬失败。
/// </summary>
internal static class RequiredMember
{
    /// <summary>查找必需方法（含继承链）。</summary>
    internal static MethodInfo Method(Type type, string name) =>
        Find(AccessTools.Method(type, name), type, name);

    /// <summary>查找必需方法（含继承链），按参数类型精确定位重载。</summary>
    internal static MethodInfo Method(
        Type type,
        string name,
        Type[] parameterTypes
    ) => Find(AccessTools.Method(type, name, parameterTypes), type, name);

    /// <summary>查找必需方法（只看声明类型自身，不看基类）。</summary>
    internal static MethodInfo DeclaredMethod(Type type, string name) =>
        Find(AccessTools.DeclaredMethod(type, name), type, name);

    /// <summary>
    /// 查找必需方法（只看声明类型自身），按参数类型精确定位重载。
    /// </summary>
    internal static MethodInfo DeclaredMethod(
        Type type,
        string name,
        Type[] parameterTypes
    ) => Find(AccessTools.DeclaredMethod(type, name, parameterTypes), type, name);

    /// <summary>查找必需属性 getter。</summary>
    internal static MethodInfo PropertyGetter(Type type, string propertyName) =>
        Find(AccessTools.PropertyGetter(type, propertyName), type, propertyName);

    private static MethodInfo Find(
        MethodInfo? method,
        Type type,
        string name
    ) => method ?? throw new MissingMethodException(
        type.FullName ?? type.Name,
        name
    );
}
