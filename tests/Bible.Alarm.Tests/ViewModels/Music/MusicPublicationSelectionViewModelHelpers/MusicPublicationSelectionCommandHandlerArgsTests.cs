#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionCommandHandlerArgsTests
{
    [Fact]
    public void TrackSelectionProgressBindings_invoke_setters()
    {
        var show = false;
        var pct = 0.0;
        var text = "";

        var sut = new TrackSelectionProgressBindings(
            SetShowProgress: v => show = v,
            SetProgressPercent: v => pct = v,
            SetProgressText: v => text = v);

        sut.SetShowProgress(true);
        sut.SetProgressPercent(0.5);
        sut.SetProgressText("hi");

        Assert.True(show);
        Assert.Equal(0.5, pct);
        Assert.Equal("hi", text);
    }

    [Fact]
    public void HandleMusicPublicationTrackSelectionArgs_round_trips_slots()
    {
        var progress = new TrackSelectionProgressBindings(_ => { }, _ => { }, _ => { });

        var current = new AlarmMusic { TrackCode = "3", PublicationCode = "iam" };
        var sut = new HandleMusicPublicationTrackSelectionArgs(
            SongPublication: null!,
            CurrentLanguage: null,
            DataProvider: null!,
            Current: current,
            Progress: progress);

        Assert.Null(sut.SongPublication);
        Assert.Null(sut.CurrentLanguage);
        Assert.Same(current, sut.Current);
        Assert.Same(progress, sut.Progress);
    }

    [Fact]
    public void HandleMusicLanguageSelectionUiCallbacks_invoke_slots()
    {
        LanguageListViewItemModel? lang = null;
        LanguageListViewItemModel? updated = null;
        var show = false;
        var pct = 0.0;
        var text = "";
        var busy = false;

        var sut = new HandleMusicLanguageSelectionUiCallbacks(
            SetCurrentLanguage: v => lang = v,
            UpdateSelectedLanguage: v => updated = v,
            SetShowProgress: v => show = v,
            SetProgressPercent: v => pct = v,
            SetProgressText: v => text = v,
            SetIsBusy: v => busy = v);

        sut.SetCurrentLanguage(null);
        Assert.Null(lang);

        sut.UpdateSelectedLanguage(null!);
        Assert.Null(updated);

        sut.SetShowProgress(true);
        sut.SetProgressPercent(1);
        sut.SetProgressText("t");
        sut.SetIsBusy(true);

        Assert.True(show);
        Assert.Equal(1, pct);
        Assert.Equal("t", text);
        Assert.True(busy);
    }
}
