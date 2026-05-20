#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class ToastArtworkImageSrcResolverBibleAlarmTests
{
    [Fact]
    public void TryResolve_returns_null_for_blank_artwork()
    {
        Assert.Null(ToastArtworkImageSrcResolver.TryResolve("  "));
    }

    [Fact]
    public void TryResolve_returns_null_for_null_artwork()
    {
        Assert.Null(ToastArtworkImageSrcResolver.TryResolve(null));
    }
}
