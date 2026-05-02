#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class AlarmViewModelArtworkHandlerTests
{
    [Fact]
    public void UpdateArtwork_SecondCallWithSameUrl_IsNoOp()
    {
        List<ImageSource?> sources = [];
        var loadingFlags = new List<bool>();
        var clearActions = 0;
        var tryLoadCount = 0;
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            s => sources.Add(s),
            loadingFlags.Add,
            () => clearActions++);

        handler.UpdateArtwork(
            "https://x/a.png",
            _ =>
            {
                tryLoadCount++;
                return true;
            },
            _ => null,
            _ => { },
            () => clearActions++);

        handler.UpdateArtwork(
            "https://x/a.png",
            _ => throw new InvalidOperationException("should not run"),
            _ => throw new InvalidOperationException("should not run"),
            _ => throw new InvalidOperationException("should not run"),
            () => throw new InvalidOperationException("should not run"));

        Assert.Equal(1, tryLoadCount);
        Assert.Equal(0, clearActions);
        Assert.True(Assert.Single(loadingFlags));
    }

    [Fact]
    public void UpdateArtwork_EmptyUrl_ClearsAndSkipsLoad()
    {
        var clearCount = 0;
        var loadingFlags = new List<bool>();
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            _ => { },
            loadingFlags.Add,
            () => clearCount++);

        handler.UpdateArtwork(
            "",
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            () => clearCount++);

        Assert.Equal(1, clearCount);
        Assert.Empty(loadingFlags);
    }

    [Fact]
    public void UpdateArtwork_NullUrl_AfterLoaded_Clears()
    {
        var clearCount = 0;
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            _ => { },
            _ => { },
            () => clearCount++);

        handler.UpdateArtwork(
            "had-url",
            _ => true,
            _ => null,
            _ => { },
            () => clearCount++);

        handler.UpdateArtwork(
            null,
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            () => clearCount++);

        Assert.Equal(1, clearCount);
    }

    [Fact]
    public void UpdateArtwork_WhenSwitchingFromPreviousUrl_ClearsBeforeNewLoad()
    {
        var clearPhases = new List<string>();
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            _ => { },
            _ => { },
            () => clearPhases.Add("bytes"));

        handler.UpdateArtwork(
            "a",
            _ =>
            {
                clearPhases.Add("tryUri");
                return true;
            },
            _ => null,
            _ => { },
            () => clearPhases.Add("fullClear"));

        handler.UpdateArtwork(
            "b",
            _ =>
            {
                clearPhases.Add("tryUriB");
                return true;
            },
            _ => null,
            _ => { },
            () => clearPhases.Add("fullClearB"));

        Assert.Equal(new[] { "tryUri", "fullClearB", "tryUriB" }, clearPhases);
    }

    [Fact]
    public void UpdateArtwork_WhenUriLoaderReturnsFalse_LoadsFromResolvedFile()
    {
        var loads = new List<string>();
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            _ => { },
            _ => { },
            () => { });

        handler.UpdateArtwork(
            "z",
            _ => false,
            _ => @"C:\art.png",
            fp => loads.Add(fp),
            () => loads.Add("cleared"));

        Assert.Equal(new[] { @"C:\art.png" }, loads);
    }

    [Fact]
    public void UpdateArtwork_WhenNoFileResolved_Clears()
    {
        var cleared = false;
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            _ => { },
            _ => { },
            () => cleared = true);

        handler.UpdateArtwork(
            "z",
            _ => false,
            _ => null,
            _ => { },
            () => cleared = true);

        Assert.True(cleared);
    }

    [Fact]
    public void UpdateArtwork_WhenLoaderThrows_Clears()
    {
        var cleared = false;
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            _ => { },
            _ => { },
            () => cleared = true);

        handler.UpdateArtwork(
            "z",
            _ => throw new IOException("disk"),
            _ => null,
            _ => { },
            () => cleared = true);

        Assert.True(cleared);
    }

    [Fact]
    public void ClearArtwork_NullsSourceAndClearsBytes()
    {
        var sources = new List<ImageSource?>();
        var byteClears = 0;
        var handler = new AlarmViewModelArtworkHandler(
            TestLogging.CreateLogger(),
            s => sources.Add(s),
            _ => { },
            () => byteClears++);

        handler.ClearArtwork();

        Assert.Null(Assert.Single(sources));
        Assert.Equal(1, byteClears);
    }
}
