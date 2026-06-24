using AtlasForge.Services;

namespace AtlasForge.Tests.Services;

public class UpdateCheckerTests
{
    [Fact]
    public void IsNewer_RemoteHigher_ReturnsTrue()
    {
        Assert.True(UpdateChecker.IsNewer("v2.0.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_SameVersion_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("v1.0.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_RemoteLower_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("v0.9.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_InvalidTag_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("invalid", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_TagWithVPrefix_ParsedCorrectly()
    {
        Assert.True(UpdateChecker.IsNewer("v1.2.3", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_EmptyTag_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("", new Version(1, 0, 0)));
    }
}