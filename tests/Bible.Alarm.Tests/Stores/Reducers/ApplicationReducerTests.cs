#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ApplicationReducerTests
{
    private static ScheduleStateItem MinimalSchedule(int id = 0, string name = "Test") =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public void OnInitialize_ReplacesScheduleListAndClearsCurrentSchedule()
    {
        var incoming = new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(1, "Loaded") };
        var prior = new ApplicationState([], currentSchedule: MinimalSchedule(99));

        var next = ApplicationReducer.OnInitialize(prior, new InitializeAction(incoming));

        Assert.Same(incoming, next.Schedules);
        Assert.Null(next.CurrentSchedule);
    }

    [Fact]
    public void OnCreateSchedule_DelegatesToScheduleCrudReducer()
    {
        var prior = new ApplicationState([]);
        var draft = MinimalSchedule(0, "From reducer");

        var next = ApplicationReducer.OnCreateSchedule(prior, new CreateScheduleAction(draft));

        Assert.Single(next.Schedules);
        Assert.Equal(-1, next.CurrentSchedule?.Id);
        Assert.Equal("From reducer", next.CurrentSchedule?.Name);
    }

    [Fact]
    public void OnUpdateScheduleFromViewModel_should_save_updates_matching_schedule_in_collection()
    {
        var existing = MinimalSchedule(5, "Old");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { existing }, currentSchedule: existing);
        var updated = MinimalSchedule(5, "Renamed");

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(updated, shouldSave: true));

        Assert.Equal("Renamed", next.Schedules!.Single(s => s.Id == 5).Name);
        Assert.Equal("Renamed", next.CurrentSchedule?.Name);
    }

    [Fact]
    public void OnUpdateScheduleFromViewModel_should_save_false_updates_current_without_changing_list()
    {
        var listItem = MinimalSchedule(5, "Persisted");
        var draft = MinimalSchedule(5, "Draft edit");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { listItem }, currentSchedule: draft);

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(draft, shouldSave: false));

        Assert.Equal("Persisted", next.Schedules!.Single().Name);
        Assert.Equal("Draft edit", next.CurrentSchedule?.Name);
    }

    [Fact]
    public void OnUpdateScheduleSuccess_delegates_to_schedule_crud_reducer()
    {
        var existing = MinimalSchedule(8, "Before");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { existing });
        var saved = MinimalSchedule(8, "After");

        var next = ApplicationReducer.OnUpdateScheduleSuccess(prior, new UpdateScheduleSuccessAction(saved));

        Assert.Equal("After", next.Schedules!.Single(s => s.Id == 8).Name);
    }

    [Fact]
    public void OnUpdateScheduleFailure_returns_original_state_reference()
    {
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(2) });
        var failed = MinimalSchedule(2, "Failed");

        var next = ApplicationReducer.OnUpdateScheduleFailure(
            prior,
            new UpdateScheduleFailureAction(failed, "error"));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnUpdateScheduleFromViewModel_null_schedule_returns_unchanged()
    {
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(1) });

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(null!, shouldSave: true));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnUpdateScheduleFromViewModel_null_schedules_collection_returns_unchanged()
    {
        var draft = MinimalSchedule(1, "Draft");
        var prior = new ApplicationState([], currentSchedule: draft);
        prior.Schedules = null!;

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(draft, shouldSave: true));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnUpdateScheduleLastPlayed_updates_list_and_current_schedule()
    {
        var playedAt = new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);
        var item = MinimalSchedule(7, "Morning");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { item }, currentSchedule: item);

        var next = ApplicationReducer.OnUpdateScheduleLastPlayed(
            prior,
            new UpdateScheduleLastPlayedAction(7, playedAt));

        Assert.Equal(playedAt, next.Schedules!.Single(s => s.Id == 7).LastPlayedAtUtc);
        Assert.Equal(playedAt, next.CurrentSchedule?.LastPlayedAtUtc);
        Assert.NotSame(item, next.Schedules!.Single());
    }

    [Fact]
    public void OnUpdateScheduleLastPlayed_no_op_when_schedule_id_not_in_collection()
    {
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(1) });

        var next = ApplicationReducer.OnUpdateScheduleLastPlayed(
            prior,
            new UpdateScheduleLastPlayedAction(99, DateTime.UtcNow));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnViewSchedule_clones_selected_schedule_and_shows_overlay()
    {
        var listItem = MinimalSchedule(3, "Listed");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { listItem });

        var next = ApplicationReducer.OnViewSchedule(prior, new ViewScheduleAction(listItem));

        Assert.NotSame(listItem, next.CurrentSchedule);
        Assert.Equal("Listed", next.CurrentSchedule?.Name);
        Assert.True(next.IsSchedulePageOverlayVisible);
        Assert.Equal(ContainerReadiness.NotReady, next.ContainerReadiness);
        Assert.Null(next.PendingScheduleLoad);
    }

    [Fact]
    public void OnViewExistingSchedule_sets_pending_load_without_current_schedule()
    {
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(4) });

        var next = ApplicationReducer.OnViewExistingSchedule(
            prior,
            new ViewExistingScheduleAction(4, isEnabled: false));

        Assert.Null(next.CurrentSchedule);
        Assert.True(next.IsSchedulePageOverlayVisible);
        Assert.Equal(new PendingScheduleLoad(4, false), next.PendingScheduleLoad);
    }

    [Fact]
    public void OnBack_clears_current_schedule_and_hides_schedule_overlay()
    {
        var prior = new ApplicationState(
            new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(1) },
            currentSchedule: MinimalSchedule(1),
            isSchedulePageOverlayVisible: true);

        var next = ApplicationReducer.OnBack(prior, new BackAction(new RecordingDisposable()));

        Assert.Null(next.CurrentSchedule);
        Assert.False(next.IsSchedulePageOverlayVisible);
    }

    [Fact]
    public void OnUpdateDraftSchedule_updates_current_when_ids_match()
    {
        var current = MinimalSchedule(11, "Before");
        var draft = MinimalSchedule(11, "Draft");
        var prior = new ApplicationState([], currentSchedule: current);

        var next = ApplicationReducer.OnUpdateDraftSchedule(prior, new UpdateDraftScheduleAction(draft));

        Assert.Equal("Draft", next.CurrentSchedule?.Name);
        Assert.NotSame(draft, next.CurrentSchedule);
    }

    [Fact]
    public void OnUpdateDraftSchedule_skips_when_schedule_id_mismatch()
    {
        var current = MinimalSchedule(11, "Current");
        var prior = new ApplicationState([], currentSchedule: current);

        var next = ApplicationReducer.OnUpdateDraftSchedule(
            prior,
            new UpdateDraftScheduleAction(MinimalSchedule(12, "Other")));

        Assert.Same(current, next.CurrentSchedule);
    }

    [Fact]
    public void OnCategorySelection_returns_unchanged_state()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);

        var next = ApplicationReducer.OnCategorySelection(
            prior,
            new CategorySelectionAction(2, "Watchtower"));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnBiblePublicationTrackSelected_merges_selection_into_current_schedule()
    {
        var current = MinimalSchedule(20, "Alarm");
        current.BiblePublicationCode = "nwt";
        current.BiblePublicationTrackCode = "1";
        var prior = new ApplicationState([], currentSchedule: current);
        var biblePub = new BiblePublicationStateItem
        {
            LanguageCode = "E",
            LanguageName = "English",
            PublicationCode = "nwtsty",
            SectionCode = "5",
            TrackCode = "12",
            TrackTitle = "Chapter 12",
            CategoryName = "Bible",
            CategoryId = 3,
        };

        var next = ApplicationReducer.OnBiblePublicationTrackSelected(
            prior,
            new TrackSelectedAction(biblePub));

        Assert.Equal("nwtsty", next.CurrentSchedule?.BiblePublicationCode);
        Assert.Equal("5", next.CurrentSchedule?.BiblePublicationSectionCode);
        Assert.Equal("12", next.CurrentSchedule?.BiblePublicationTrackCode);
        Assert.Equal("Chapter 12", next.CurrentSchedule?.BiblePublicationTrackTitle);
        Assert.Equal("Bible", next.CurrentSchedule?.BiblePublicationCategoryName);
    }

    [Fact]
    public void OnUpdateScheduleFromViewModel_should_save_adds_saved_schedule_missing_from_list()
    {
        var existing = MinimalSchedule(1, "Keep");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { existing });
        var saved = MinimalSchedule(50, "Inserted on save");

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(saved, shouldSave: true));

        Assert.Contains(next.Schedules!, s => s.Id == 50 && s.Name == "Inserted on save");
    }

    private sealed class RecordingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
