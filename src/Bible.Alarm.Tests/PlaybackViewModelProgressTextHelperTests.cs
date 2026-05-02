#nullable enable

using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelProgressTextHelperTests
{
    [Fact]
    public void All_tracks_loaded_returns_100_percent()
    {
        var text = PlaybackViewModelProgressTextHelper.GetProgressText(5, 5, 0, null, 0);

        Assert.Equal("100%", text);
    }

    [Fact]
    public void Bytes_totals_show_download_percentage_when_tracks_remain()
    {
        var text = PlaybackViewModelProgressTextHelper.GetProgressText(1, 5, 250, 1000L, 0);

        Assert.Equal("25%", text);
    }

    [Fact]
    public void Preparation_progress_used_when_byte_totals_unavailable()
    {
        var text = PlaybackViewModelProgressTextHelper.GetProgressText(1, 5, 0, null, 0.37);

        Assert.Equal("37%", text);
    }

    [Fact]
    public void Falls_back_to_zero_percent_when_no_signals()
    {
        var text = PlaybackViewModelProgressTextHelper.GetProgressText(1, 5, 0, null, 0);

        Assert.Equal("0%", text);
    }

    [Fact]
    public void Zero_expected_bytes_falls_through_to_track_based_progress()
    {
        var text = PlaybackViewModelProgressTextHelper.GetProgressText(2, 10, 100, 0L, 0.5);

        Assert.Equal("50%", text);
    }
}
