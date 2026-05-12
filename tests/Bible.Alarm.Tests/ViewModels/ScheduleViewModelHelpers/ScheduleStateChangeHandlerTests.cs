#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateChangeHandlerTests
{
    [Fact]
    public void HandleScheduleUpdateFromState_returns_false_when_schedule_null()
    {
        var sut = new ScheduleStateChangeHandler(TestLogging.CreateLogger());
        string? track = null;
        string? pub = null;
        string? lang = null;
        bool? repeat = null;

        var result = sut.HandleScheduleUpdateFromState(null, ref track, ref pub, ref lang, ref repeat, out var hasChanges);

        Assert.False(result);
        Assert.False(hasChanges);
        Assert.Null(track);
    }

    [Fact]
    public void HandleScheduleUpdateFromState_sets_hasChanges_and_updates_refs_when_music_fields_differ()
    {
        var sut = new ScheduleStateChangeHandler(TestLogging.CreateLogger());
        string? track = "a";
        string? pub = "b";
        string? lang = "c";
        bool? repeat = false;

        var schedule = new ScheduleStateItem
        {
            MusicTrackCode = "x",
            MusicPublicationCode = "y",
            MusicLanguageCode = "z",
            MusicRepeat = true
        };

        var result = sut.HandleScheduleUpdateFromState(schedule, ref track, ref pub, ref lang, ref repeat, out var hasChanges);

        Assert.True(result);
        Assert.True(hasChanges);
        Assert.Equal("x", track);
        Assert.Equal("y", pub);
        Assert.Equal("z", lang);
        Assert.Equal(true, repeat);
    }

    [Fact]
    public void HandleScheduleUpdateFromState_returns_false_when_music_unchanged()
    {
        var sut = new ScheduleStateChangeHandler(TestLogging.CreateLogger());
        string? track = "same";
        string? pub = "pub";
        string? lang = "E";
        bool? repeat = false;
        var schedule = new ScheduleStateItem
        {
            MusicTrackCode = track,
            MusicPublicationCode = pub,
            MusicLanguageCode = lang,
            MusicRepeat = repeat
        };

        var result = sut.HandleScheduleUpdateFromState(schedule, ref track, ref pub, ref lang, ref repeat, out var hasChanges);

        Assert.False(result);
        Assert.False(hasChanges);
    }
}
