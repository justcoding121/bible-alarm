#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class BootstrapLogRootResolverTests
{
    [Fact]
    public void TryGetLogRoot_prefers_packaged_path_when_it_returns_nonempty_text()
    {
        Assert.True(BootstrapLogRootResolver.TryGetLogRoot(
            out var root,
            () => @"C:\packaged-cache",
            () => @"D:\exe"));

        Assert.Equal(@"C:\packaged-cache", root);
    }

    [Fact]
    public void TryGetLogRoot_falls_back_when_packaged_probe_throws()
    {
        Assert.True(BootstrapLogRootResolver.TryGetLogRoot(
            out var root,
            () => throw new InvalidOperationException(),
            () => @"D:\exe"));

        Assert.Equal(@"D:\exe", root);
    }

    [Fact]
    public void TryGetLogRoot_returns_false_when_both_probes_throw()
    {
        Assert.False(BootstrapLogRootResolver.TryGetLogRoot(
            out _,
            () => throw new InvalidOperationException(),
            () => throw new IOException()));
    }
}
