#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionStateManagerTests
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
        if (current != null)
        {
            set.Add(current);
        }

        return new ApplicationState(set, currentSchedule: current);
    }

    [Fact]
    public async Task HandleBiblePublicationInitialized_loads_current_and_runs_initialize_once()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "3",
            BiblePublicationFinishedDuration = TimeSpan.FromSeconds(12),
        };
        var state = new FakeAppState(MakeState(schedule));
        var sut = new TrackSelectionStateManager();
        var calls = 0;

        sut.HandleBiblePublicationInitialized(
            state,
            _ => { },
            async (_, _, _) =>
            {
                Interlocked.Increment(ref calls);
                await Task.CompletedTask;
            });

        await Task.Delay(300);

        Assert.Equal(1, calls);
        Assert.True(sut.InitComplete);
        Assert.NotNull(sut.Current);
        Assert.Equal("40", sut.Current!.SectionCode);
        Assert.Equal("3", sut.Current.TrackCode);

        sut.HandleBiblePublicationInitialized(
            state,
            _ => { },
            async (_, _, _) =>
            {
                Interlocked.Increment(ref calls);
                await Task.CompletedTask;
            });

        await Task.Delay(100);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void HandleBiblePublicationChanged_no_ops_when_current_schedule_null()
    {
        var sut = new TrackSelectionStateManager();
        var state = new FakeAppState(MakeState(null));

        sut.HandleBiblePublicationChanged(
            state,
            _ => { },
            (_, _, _) => Task.CompletedTask,
            () => throw new InvalidOperationException("should not select"));

        Assert.Null(sut.Current);
    }

    [Fact]
    public void UpdateFromState_requires_non_whitespace_section()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = "1",
        };
        var sut = new TrackSelectionStateManager();
        sut.UpdateFromState(new FakeAppState(MakeState(schedule)));

        Assert.Null(sut.Current);
    }

    [Fact]
    public void UpdateFromState_sets_current_when_section_present()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionCode = "41",
            BiblePublicationTrackCode = "9",
        };
        var sut = new TrackSelectionStateManager();
        sut.UpdateFromState(new FakeAppState(MakeState(schedule)));

        Assert.NotNull(sut.Current);
        Assert.Equal("41", sut.Current!.SectionCode);
        Assert.Equal("9", sut.Current.TrackCode);
    }

    [Fact]
    public void UpdateFromStateForNonSectioned_allows_missing_section()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = AppConstants.Media.MediatorPublicationCodeVODBibleTeachings,
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = "10",
        };
        var sut = new TrackSelectionStateManager();
        sut.UpdateFromStateForNonSectioned(new FakeAppState(MakeState(schedule)));

        Assert.NotNull(sut.Current);
        Assert.Null(sut.Current!.SectionCode);
    }
}
