#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class BootstrapLogRootResolverBibleAlarmTests
{
    [Fact]
    public void TryGetLogRoot_returns_true_when_packaged_path_nonempty()
    {
        Assert.True(BootstrapLogRootResolver.TryGetLogRoot(
            out var root,
            () => @"C:\packaged-logs",
            () => @"D:\fallback"));

        Assert.Equal(@"C:\packaged-logs", root);
    }

    [Fact]
    public void TryGetLogRoot_falls_back_when_packaged_probe_throws()
    {
        Assert.True(BootstrapLogRootResolver.TryGetLogRoot(
            out var root,
            () => throw new InvalidOperationException(),
            () => @"D:\exe-fallback"));

        Assert.Equal(@"D:\exe-fallback", root);
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
