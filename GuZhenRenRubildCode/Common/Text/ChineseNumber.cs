namespace GuZhenRenRubild.Common.Text;

/// <summary>
/// 把 0~9 的阿拉伯数字换成中文数字，供卡面"文言风格"头部使用
/// （例：三转、冷却二）。
///
/// 超出范围的值原样返回阿拉伯数字，因此任何数值都至少能渲染成可读文本，
/// 不会因为越界而出现空白。蛊牌转数上限为 9、冷却回合数为个位数，
/// 这个范围已经覆盖当前全部卡面文案。
/// </summary>
public static class ChineseNumber
{
    private static readonly string[] Digits =
    [
        "零", "一", "二", "三", "四", "五", "六", "七", "八", "九",
    ];

    /// <summary>
    /// 返回 <paramref name="value"/> 的中文数字写法；非 0~9 的值原样返回阿拉伯数字。
    /// </summary>
    public static string ToChineseNumber(int value) =>
        value >= 0 && value < Digits.Length
            ? Digits[value]
            : value.ToString();
}
