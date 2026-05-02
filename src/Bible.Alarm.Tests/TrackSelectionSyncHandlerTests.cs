#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using BiblePubTrackSelectedAction = Bible.Alarm.Stores.Actions.BiblePublications.TrackSelectedAction;
using MusicTrackSelectedAction = Bible.Alarm.Stores.Actions.Music.TrackSelectedAction;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionSyncHandlerTests
{
    private sealed class FakeApplicationState : IState<ApplicationState>
    {
        public FakeApplicationState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; }

#pragma warning disable CS0067 // Event is required by IState<T>
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : Fluxor.IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private static ScheduleStateItem BuildSchedule(int id = 1, int? musicId = null)
    {
        return new ScheduleStateItem
        {
            Id = id,
            Name = "Test",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.All,
            NotificationEnabled = true,
            MusicEnabled = true,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
            MusicId = musicId,
            MusicLanguageCode = AppConstants.Media.DefaultLanguageCode,
            MusicPublicationCode = "iam-1",
            MusicSectionCode = "1",
            MusicTrackCode = "1",
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "1"
        };
    }

    [Fact]
    public async Task Bible_HandleTrackSelected_NoDispatch_When_CurrentSchedule_Null()
    {
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: null));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        await sut.HandleTrackSelected(
            new BiblePubTrackSelectedAction(new BiblePublicationStateItem
            {
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "2",
                CategoryName = AppConstants.Media.BiblePublicationCategoryBible
            }),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task Bible_HandleTrackSelected_NoDispatch_When_Already_In_Sync()
    {
        var schedule = BuildSchedule();
        var state = new FakeApplicationState(new ApplicationState([], schedule));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        var bibleItem = new BiblePublicationStateItem
        {
            LanguageCode = schedule.BiblePublicationLanguageCode!,
            PublicationCode = schedule.BiblePublicationCode!,
            SectionCode = schedule.BiblePublicationSectionCode,
            TrackCode = schedule.BiblePublicationTrackCode!,
            CategoryName = AppConstants.Media.BiblePublicationCategoryBible
        };

        await sut.HandleTrackSelected(new BiblePubTrackSelectedAction(bibleItem), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task Bible_HandleTrackSelected_NoDispatch_When_Action_BiblePublication_Null()
    {
        var schedule = BuildSchedule();
        var state = new FakeApplicationState(new ApplicationState([], schedule));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        await sut.HandleTrackSelected(new BiblePubTrackSelectedAction(null!), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task Bible_HandleTrackSelected_Dispatches_Update_When_Track_Changes()
    {
        var schedule = BuildSchedule();
        var state = new FakeApplicationState(new ApplicationState([], schedule));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        var bibleItem = new BiblePublicationStateItem
        {
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "99",
            CategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            PublicationName = "NWT",
            SectionName = "Matthew",
            TrackTitle = "Matt 1"
        };

        await sut.HandleTrackSelected(new BiblePubTrackSelectedAction(bibleItem), dispatcher);

        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.False(update.MusicUpdated);
        Assert.True(update.BiblePublicationUpdated);
        Assert.False(update.ShouldSave);
        Assert.Equal("99", update.Schedule.BiblePublicationTrackCode);
    }

    [Fact]
    public async Task Music_HandleTrackSelected_NoDispatch_When_CurrentSchedule_Null()
    {
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: null));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        await sut.HandleTrackSelected(
            new MusicTrackSelectedAction(new MusicStateItem
            {
                Id = 0,
                PublicationCode = "iam-1",
                LanguageCode = null,
                TrackCode = "2"
            }),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task Music_HandleTrackSelected_NoDispatch_When_Id_Mismatch_And_Language_Unchanged()
    {
        var schedule = BuildSchedule(musicId: 10);
        var state = new FakeApplicationState(new ApplicationState([], schedule));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        await sut.HandleTrackSelected(
            new MusicTrackSelectedAction(new MusicStateItem
            {
                Id = 999,
                PublicationCode = "iam-1",
                LanguageCode = AppConstants.Media.DefaultLanguageCode,
                TrackCode = "5",
                LanguageName = "English"
            }),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task Music_HandleTrackSelected_Dispatches_When_New_Selection_Id_Zero()
    {
        var schedule = BuildSchedule(musicId: 10);
        var state = new FakeApplicationState(new ApplicationState([], schedule));
        var sut = new TrackSelectionSyncHandler(state);
        var dispatcher = new RecordingDispatcher();

        await sut.HandleTrackSelected(
            new MusicTrackSelectedAction(new MusicStateItem
            {
                Id = 0,
                PublicationCode = "iam-2",
                LanguageCode = null,
                SectionCode = "2",
                TrackCode = "3",
                PublicationName = "Disc 2",
                SectionName = "Sec",
                TrackName = "Track three",
                LanguageName = string.Empty
            }),
            dispatcher);

        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.True(update.MusicUpdated);
        Assert.False(update.BiblePublicationUpdated);
        Assert.Equal("iam-2", update.Schedule.MusicPublicationCode);
        Assert.Equal("3", update.Schedule.MusicTrackCode);
    }
}
