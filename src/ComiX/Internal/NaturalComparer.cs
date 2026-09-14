namespace ComiX.Internal;

/// <summary>
/// Compares strings so that embedded numbers order numerically: <c>2.jpg</c> before <c>10.jpg</c>,
/// and <c>page-2</c> before <c>page-10</c>.
/// </summary>
/// <remarks>
/// Both strings are traversed in parallel; each run of digits is compared as a number and all other
/// characters case-insensitively. Leading zeros do not affect numeric value, so <c>001.jpg</c> and
/// <c>1.jpg</c> compare equal on that run and are then ordered by an ordinal comparison.
/// </remarks>
internal sealed class NaturalComparer : IComparer<string>
{
    public static NaturalComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var comparison = CompareNumberRun(x, ref i, y, ref j);
                if (comparison != 0)
                {
                    return comparison;
                }

                continue;
            }

            var charComparison = CompareChar(x[i], y[j]);
            if (charComparison != 0)
            {
                return charComparison;
            }

            i++;
            j++;
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }

    private static int CompareChar(char a, char b)
    {
        var lowerA = char.ToLowerInvariant(a);
        var lowerB = char.ToLowerInvariant(b);
        return lowerA.CompareTo(lowerB);
    }

    private static int CompareNumberRun(string x, ref int i, string y, ref int j)
    {
        var startX = i;
        var startY = j;
        while (i < x.Length && char.IsAsciiDigit(x[i]))
        {
            i++;
        }

        while (j < y.Length && char.IsAsciiDigit(y[j]))
        {
            j++;
        }

        var runX = x.AsSpan(startX, i - startX).TrimStart('0');
        var runY = y.AsSpan(startY, j - startY).TrimStart('0');

        return runX.Length != runY.Length
            ? runX.Length.CompareTo(runY.Length)
            : runX.SequenceCompareTo(runY);
    }
}
