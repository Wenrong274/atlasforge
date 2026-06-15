using System.IO;

namespace AtlasForge.Services;

public class NaturalSortComparer : IComparer<string>
{
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

        return CompareNatural(Path.GetFileName(x), Path.GetFileName(y));
    }

    private static int CompareNatural(string x, string y)
    {
        var xIndex = 0;
        var yIndex = 0;

        while (xIndex < x.Length && yIndex < y.Length)
        {
            var xIsDigit = char.IsDigit(x[xIndex]);
            var yIsDigit = char.IsDigit(y[yIndex]);

            if (xIsDigit && yIsDigit)
            {
                var result = CompareNumberSegment(x, ref xIndex, y, ref yIndex);
                if (result != 0)
                {
                    return result;
                }

                continue;
            }

            var xChar = char.ToUpperInvariant(x[xIndex]);
            var yChar = char.ToUpperInvariant(y[yIndex]);
            if (xChar != yChar)
            {
                return xChar.CompareTo(yChar);
            }

            xIndex++;
            yIndex++;
        }

        return x.Length.CompareTo(y.Length);
    }

    private static int CompareNumberSegment(string x, ref int xIndex, string y, ref int yIndex)
    {
        var xStart = xIndex;
        var yStart = yIndex;

        while (xIndex < x.Length && char.IsDigit(x[xIndex]))
        {
            xIndex++;
        }

        while (yIndex < y.Length && char.IsDigit(y[yIndex]))
        {
            yIndex++;
        }

        var xNumber = x.AsSpan(xStart, xIndex - xStart).TrimStart('0');
        var yNumber = y.AsSpan(yStart, yIndex - yStart).TrimStart('0');

        if (xNumber.Length != yNumber.Length)
        {
            return xNumber.Length.CompareTo(yNumber.Length);
        }

        var valueCompare = xNumber.SequenceCompareTo(yNumber);
        if (valueCompare != 0)
        {
            return valueCompare;
        }

        return (xIndex - xStart).CompareTo(yIndex - yStart);
    }
}
