#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackStateManagerTests
{
    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static ApplicationState MakeState(ScheduleStateItem? current)
    {
        var set = new ObservableHashSet<ScheduleStateItem>();
        if (current != null)
        {
            set.Add(current);
        }

        return new ApplicationState(set, currentSchedule: current);
    }

    [Fact]
    public void InitializeCurrent_populates_alarm_music_from_schedule_publication_code()
    {
        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = "sjj",
            MusicLanguageCode = "E",
            MusicSectionCode = "1",
            MusicTrackCode = "3",
            MusicRepeat = true,
        };
        var sut = new MusicTrackStateManager();
        sut.InitializeCurrent(new FakeAppState(MakeState(schedule)));

        Assert.NotNull(sut.Current);
        Assert.Equal("sjj", sut.Current!.PublicationCode);
        Assert.Equal("E", sut.Current.LanguageCode);
        Assert.Equal("3", sut.Current.TrackCode);
        Assert.True(sut.Current.Repeat);
    }

    [Fact]
    public void InitializeCurrent_leaves_current_null_when_publication_code_missing()
    {
        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = null,
            MusicLanguageCode = "E",
        };
        var sut = new MusicTrackStateManager();
        sut.InitializeCurrent(new FakeAppState(MakeState(schedule)));

        Assert.Null(sut.Current);
    }

    [Fact]
    public void InitializeCurrent_leaves_current_null_when_no_current_schedule()
    {
        var sut = new MusicTrackStateManager();
        sut.InitializeCurrent(new FakeAppState(MakeState(null)));

        Assert.Null(sut.Current);
    }
}
