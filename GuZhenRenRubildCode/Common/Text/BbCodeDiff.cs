using System.Text;

namespace GuZhenRenRubild.Common.Text;

/// <summary>
/// 对比两段"已经渲染过 BBCode"的文本，把后者中新增或发生变化的可见片段
/// 用 <c>[green]...[/green]</c> 标出来。
///
/// 典型用途是升级/升转预览：先记录改动前的描述文本，改动后重新渲染，
/// 再把差异染色显示。算法只处理可见字符，已有的 BBCode 标签原样保留，
/// 因此不会破坏游戏自己的富文本语义。差异匹配使用最长公共子序列，
/// 与语言无关（中英文都按字符/词块切分），可以直接复用于任何预览场景。
/// </summary>
public static class BbCodeDiff
{
    private readonly record struct DescriptionToken(
        int Start,
        int Length,
        string Text,
        bool IsWhitespace
    );

    /// <summary>
    /// 返回 <paramref name="afterFormatted"/> 的染色版本；两者没有可见差异时原样返回。
    /// </summary>
    public static string HighlightAddedOrChanged(
        string beforeFormatted,
        string afterFormatted
    )
    {
        ArgumentNullException.ThrowIfNull(beforeFormatted);
        ArgumentNullException.ThrowIfNull(afterFormatted);

        string before = StripBbCode(beforeFormatted);
        string after = StripBbCode(afterFormatted);
        List<DescriptionToken> beforeTokens = Tokenize(before);
        List<DescriptionToken> afterTokens = Tokenize(after);
        bool[] matchedAfter = FindMatchedAfterTokens(
            beforeTokens,
            afterTokens
        );
        bool[] highlighted = new bool[after.Length];

        for (int index = 0; index < afterTokens.Count; index++)
        {
            DescriptionToken token = afterTokens[index];
            if (matchedAfter[index] || token.IsWhitespace)
            {
                continue;
            }

            Array.Fill(
                highlighted,
                true,
                token.Start,
                token.Length
            );
        }

        // 两个变化片段之间只有空白时一并着色，英文预览不会出现
        // 逐词断开的绿色标签。
        for (int index = 0; index < highlighted.Length; index++)
        {
            if (highlighted[index] || !char.IsWhiteSpace(after[index]))
            {
                continue;
            }

            int end = index;
            while (end < highlighted.Length &&
                   char.IsWhiteSpace(after[end]))
            {
                end++;
            }

            bool leftChanged = index > 0 && highlighted[index - 1];
            bool rightChanged =
                end < highlighted.Length && highlighted[end];
            if (leftChanged && rightChanged)
            {
                Array.Fill(
                    highlighted,
                    true,
                    index,
                    end - index
                );
            }

            index = end - 1;
        }

        if (!highlighted.Any(value => value))
        {
            return afterFormatted;
        }

        return ApplyVisibleHighlights(afterFormatted, highlighted);
    }

    private static bool[] FindMatchedAfterTokens(
        IReadOnlyList<DescriptionToken> before,
        IReadOnlyList<DescriptionToken> after
    )
    {
        int[,] lengths = new int[before.Count + 1, after.Count + 1];

        for (int left = before.Count - 1; left >= 0; left--)
        {
            for (int right = after.Count - 1; right >= 0; right--)
            {
                lengths[left, right] =
                    before[left].Text == after[right].Text
                        ? lengths[left + 1, right + 1] + 1
                        : Math.Max(
                            lengths[left + 1, right],
                            lengths[left, right + 1]
                        );
            }
        }

        bool[] matched = new bool[after.Count];
        int beforeIndex = 0;
        int afterIndex = 0;

        while (beforeIndex < before.Count &&
               afterIndex < after.Count)
        {
            if (before[beforeIndex].Text == after[afterIndex].Text)
            {
                matched[afterIndex] = true;
                beforeIndex++;
                afterIndex++;
            }
            else if (lengths[beforeIndex + 1, afterIndex] >=
                     lengths[beforeIndex, afterIndex + 1])
            {
                beforeIndex++;
            }
            else
            {
                afterIndex++;
            }
        }

        return matched;
    }

    private static List<DescriptionToken> Tokenize(string text)
    {
        List<DescriptionToken> result = [];
        int index = 0;

        while (index < text.Length)
        {
            int start = index;
            bool whitespace = char.IsWhiteSpace(text[index]);
            bool word = char.IsLetterOrDigit(text[index]) ||
                text[index] == '_' ||
                text[index] == '%';
            index++;

            while (index < text.Length)
            {
                bool nextWhitespace = char.IsWhiteSpace(text[index]);
                bool nextWord = char.IsLetterOrDigit(text[index]) ||
                    text[index] == '_' ||
                    text[index] == '%';

                if (whitespace != nextWhitespace ||
                    (!whitespace && word != nextWord) ||
                    (!whitespace && !word))
                {
                    break;
                }

                index++;
            }

            result.Add(
                new DescriptionToken(
                    start,
                    index - start,
                    text[start..index],
                    whitespace
                )
            );
        }

        return result;
    }

    private static string StripBbCode(string formatted)
    {
        StringBuilder result = new(formatted.Length);

        for (int index = 0; index < formatted.Length; index++)
        {
            if (formatted[index] == '[')
            {
                int tagEnd = formatted.IndexOf(']', index + 1);
                if (tagEnd >= 0)
                {
                    index = tagEnd;
                    continue;
                }
            }

            result.Append(formatted[index]);
        }

        return result.ToString();
    }

    private static string ApplyVisibleHighlights(
        string formatted,
        IReadOnlyList<bool> highlighted
    )
    {
        StringBuilder result = new(formatted.Length + 64);
        int visibleIndex = 0;
        bool greenOpen = false;

        for (int index = 0; index < formatted.Length; index++)
        {
            if (formatted[index] == '[')
            {
                int tagEnd = formatted.IndexOf(']', index + 1);
                if (tagEnd >= 0)
                {
                    if (greenOpen)
                    {
                        result.Append("[/green]");
                        greenOpen = false;
                    }

                    result.Append(
                        formatted,
                        index,
                        tagEnd - index + 1
                    );
                    index = tagEnd;
                    continue;
                }
            }

            bool shouldBeGreen =
                visibleIndex < highlighted.Count &&
                highlighted[visibleIndex];

            if (shouldBeGreen && !greenOpen)
            {
                result.Append("[green]");
                greenOpen = true;
            }
            else if (!shouldBeGreen && greenOpen)
            {
                result.Append("[/green]");
                greenOpen = false;
            }

            result.Append(formatted[index]);
            visibleIndex++;
        }

        if (greenOpen)
        {
            result.Append("[/green]");
        }

        return result.ToString();
    }
}
