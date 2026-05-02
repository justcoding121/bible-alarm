#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class AssemblyAppVersionFinderTests
{
    [Fact]
    public void GetVersionName_returns_non_empty_and_stable()
    {
        var sut = new AssemblyAppVersionFinder();

        var v1 = sut.GetVersionName();
        var v2 = sut.GetVersionName();

        Assert.False(string.IsNullOrWhiteSpace(v1));
        Assert.Equal(v1, v2);
    }
}
