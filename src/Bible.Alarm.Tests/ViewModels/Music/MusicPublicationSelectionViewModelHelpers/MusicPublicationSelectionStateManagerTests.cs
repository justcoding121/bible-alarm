#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionStateManagerTests
{
    private sealed class FakeAppState : IState<ApplicationState>
    {
        public FakeAppState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static ApplicationState MakeState(ScheduleStateItem? current)
    {
        var set = new ObservableHashSet<ScheduleStateItem>();
        return new ApplicationState(set, currentSchedule: current);
    }

    [Fact]
    public void GetCurrentFromState_null_when_no_music_publication()
    {
        var state = new FakeAppState(MakeState(new ScheduleStateItem { Id = 1, MusicPublicationCode = "" }));

        Assert.Null(MusicPublicationSelectionStateManager.GetCurrentFromState(state));
    }

    [Fact]
    public void GetCurrentFromState_builds_alarm_music_from_schedule()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 2,
            MusicPublicationCode = "osg",
            MusicLanguageCode = "E",
            MusicTrackCode = "12",
            MusicRepeat = true,
        };
        var state = new FakeAppState(MakeState(schedule));

        var current = MusicPublicationSelectionStateManager.GetCurrentFromState(state);

        Assert.NotNull(current);
        Assert.Equal("osg", current!.PublicationCode);
        Assert.Equal("E", current.LanguageCode);
        Assert.Equal("12", current.TrackCode);
        Assert.True(current.Repeat);
    }

    [Fact]
    public void InitializeCurrent_sets_current_from_fluxor_schedule()
    {
        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            MusicLanguageCode = null,
            MusicTrackCode = "3",
            MusicRepeat = false,
        };
        var sut = new MusicPublicationSelectionStateManager();
        sut.InitializeCurrent(new FakeAppState(MakeState(schedule)));

        Assert.NotNull(sut.Current);
        Assert.Null(sut.Current!.LanguageCode);
        Assert.Equal(AppConstants.Media.MelodyMusicPublicationCodeIam, sut.Current.PublicationCode);
    }

    [Fact]
    public void EnsureCurrentIsSet_derives_when_current_null()
    {
        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = "vocals",
            MusicLanguageCode = "E",
            MusicTrackCode = "1",
        };
        var sut = new MusicPublicationSelectionStateManager();
        sut.EnsureCurrentIsSet(new FakeAppState(MakeState(schedule)));

        Assert.Equal("vocals", sut.Current!.PublicationCode);
    }

    [Fact]
    public async Task HandleMusicInitialized_runs_initialize_once()
    {
        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = "p",
            MusicLanguageCode = "EN",
            MusicTrackCode = "2",
        };
        var state = new FakeAppState(MakeState(schedule));
        var sut = new MusicPublicationSelectionStateManager();
        var calls = 0;

        sut.HandleMusicInitialized(state, _ => { }, async () =>
        {
            Interlocked.Increment(ref calls);
            await Task.CompletedTask;
        });

        await Task.Delay(200);

        Assert.Equal(1, calls);
        Assert.True(sut.InitComplete);
        Assert.Equal("EN", sut.LastLanguageCode);
    }

    [Fact]
    public void HandleMusicChanged_no_ops_when_current_schedule_missing()
    {
        var sut = new MusicPublicationSelectionStateManager();

        sut.HandleMusicChanged(
            new FakeAppState(MakeState(null)),
            _ => { },
            _ => Task.CompletedTask,
            () => throw new InvalidOperationException("should not select"));
    }

    [Fact]
    public async Task HandleMusicChanged_returns_when_init_complete_and_language_unchanged()
    {
        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = "pub",
            MusicLanguageCode = "E",
            MusicTrackCode = "1",
        };
        var state = new FakeAppState(MakeState(schedule));
        var sut = new MusicPublicationSelectionStateManager();

        sut.HandleMusicInitialized(state, _ => { }, () => Task.CompletedTask);
        await Task.Delay(150);

        var selectedCalls = 0;
        sut.HandleMusicChanged(
            state,
            _ => { },
            _ => Task.CompletedTask,
            () => selectedCalls++);

        Assert.Equal(0, selectedCalls);
    }
}
