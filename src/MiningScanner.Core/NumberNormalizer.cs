using System.Globalization;

namespace MiningScanner.Core;

/// <summary>
/// Accepts one positive integer, optionally in exact groups of three. No OCR
/// character repair, decimal rounding, approximation or substring extraction.
/// </summary>
public static class NumberNormalizer
{
    public const int MaximumValue = 999_999_999;

    public static bool TryNormalize(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 64)
            return false;

        // OCR engines commonly append one line ending. Interior line endings
        // remain invalid, so multiple lines can never silently become one value.
        ReadOnlySpan<char> input = text.AsSpan().Trim();
        if (input.IsEmpty || input[0] == '0')
            return false;

        Span<char> digits = stackalloc char[9];
        int digitCount = 0;
        int groupLength = 0;
        int completedGroups = 0;
        char separator = '\0';

        foreach (char ch in input)
        {
            if (ch is >= '0' and <= '9')
            {
                if (digitCount >= digits.Length)
                    return false;
                digits[digitCount++] = ch;
                groupLength++;
                continue;
            }

            if (!IsThousandsSeparator(ch))
                return false;
            if (separator == '\0')
                separator = ch;
            if (ch != separator || groupLength == 0)
                return false;
            if (completedGroups == 0 ? groupLength > 3 : groupLength != 3)
                return false;
            completedGroups++;
            groupLength = 0;
        }

        if (digitCount == 0 || (completedGroups > 0 && groupLength != 3))
            return false;

        return int.TryParse(digits[..digitCount], NumberStyles.None,
                   CultureInfo.InvariantCulture, out value)
               && value is >= 1 and <= MaximumValue;
    }

    private static bool IsThousandsSeparator(char ch) =>
        ch is '.' or ',' or '\'' or '\u2019' or ' ' or '\u00A0' or '\u2009' or '\u202F';
}
