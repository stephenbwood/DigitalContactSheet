namespace DigitalContactSheet.Core.Scanning;

/// <summary>
/// Case-insensitive, numeric-aware string comparison so that "img2" sorts before "img10".
/// </summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

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
            var cx = x[i];
            var cy = y[j];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                // Compare the full run of digits numerically (ignoring leading zeros).
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsDigit(y[j]))
                {
                    j++;
                }

                var numX = x.AsSpan(startX, i - startX).TrimStart('0');
                var numY = y.AsSpan(startY, j - startY).TrimStart('0');

                if (numX.Length != numY.Length)
                {
                    return numX.Length - numY.Length;
                }

                var cmp = numX.SequenceCompareTo(numY);
                if (cmp != 0)
                {
                    return cmp;
                }

                continue;
            }

            var c = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
            if (c != 0)
            {
                return c;
            }

            i++;
            j++;
        }

        return (x.Length - i) - (y.Length - j);
    }
}
