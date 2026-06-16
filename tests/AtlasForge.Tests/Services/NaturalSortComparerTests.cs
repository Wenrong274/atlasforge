using AtlasForge.Services;

namespace AtlasForge.Tests.Services;

public class NaturalSortComparerTests
{
    private readonly NaturalSortComparer _comparer = new();

    [Fact]
    public void Sort_NumericSuffix_OrdersNumerically()
    {
        var files = new[] { "fire_10.png", "fire_2.png", "fire_1.png" };

        Array.Sort(files, _comparer);

        Assert.Equal(["fire_1.png", "fire_2.png", "fire_10.png"], files);
    }

    [Fact]
    public void Sort_NoNumbers_OrdersAlphabetically()
    {
        var files = new[] { "c.png", "a.png", "b.png" };

        Array.Sort(files, _comparer);

        Assert.Equal(["a.png", "b.png", "c.png"], files);
    }

    [Fact]
    public void Sort_MixedPaths_SortsOnFilenameSegments()
    {
        var files = new[] { "D:/fx/fire_3.png", "D:/fx/fire_1.png", "D:/fx/fire_2.png" };

        Array.Sort(files, _comparer);

        Assert.Equal(["D:/fx/fire_1.png", "D:/fx/fire_2.png", "D:/fx/fire_3.png"], files);
    }
}