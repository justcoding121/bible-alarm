#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCommandExecutorTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class RecordingScheduleCommandService : IScheduleCommandService
    {
        public List<(bool IsNew, int ScheduleId)> CancelCalls { get; } = [];
        public List<(bool IsNew, int ScheduleId, int ScheduleCount)> DeleteCalls { get; } = [];
        public List<(bool IsNew, int ScheduleId)> SaveCalls { get; } = [];

        public Task ExecuteCancelAsync(bool isNewSchedule, int scheduleId, ScheduleStateItem? currentSchedule)
        {
            CancelCalls.Add((isNewSchedule, scheduleId));
            return Task.CompletedTask;
        }

        public Task<bool> ExecuteSaveAsync(bool isNewSchedule, int scheduleId, ScheduleStateItem currentSchedule, bool musicUpdated, bool biblePublicationUpdated, bool modelInitialized)
        {
            SaveCalls.Add((isNewSchedule, scheduleId));
            return Task.FromResult(true);
        }

        public Task<bool> ExecuteDeleteAsync(bool isNewSchedule, int scheduleId, int scheduleCount)
        {
            DeleteCalls.Add((isNewSchedule, scheduleId, scheduleCount));
            return Task.FromResult(true);
        }

        public Task ValidateNotificationPermissionsAsync(ScheduleStateItem? currentSchedule) =>
            Task.CompletedTask;

        public List<(bool IsNew, bool IsPreparing, int ScheduleId, int PlaybackScheduleId)> StopPlaybackCalls { get; } = [];

        public Task StopPlaybackIfNeededAsync(bool isNewSchedule, bool isPreparingOrPlaying, int scheduleId, int currentPlaybackScheduleId)
        {
            StopPlaybackCalls.Add((isNewSchedule, isPreparingOrPlaying, scheduleId, currentPlaybackScheduleId));
            return Task.CompletedTask;
        }

        public Task HandleSaveResultAsync(bool saved, int scheduleId, bool isEnabled, AlarmSchedule model) =>
            Task.CompletedTask;
    }

    private sealed class NoOpScheduleMediaCacheService : IScheduleMediaCacheService
    {
        public void SetupMediaCache(int scheduleId, bool isUpdate = false)
        {
        }
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem Schedule(int id) =>
        new()
        {
            Id = id,
            Name = "Test",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
        };

    [Fact]
    public async Task CancelCommand_Invokes_Service_With_CurrentScheduleId()
    {
        var schedule = Schedule(42);
        var appState = new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule);
        var commands = new RecordingScheduleCommandService();
        var cancelBusyFlags = new List<bool>();

        var sut = new ScheduleCommandExecutor(
            new ScheduleCommandExecutorCoreDeps(
                commands,
                new NoOpScheduleMediaCacheService(),
                new FakeApplicationState(appState),
                new FakePlaybackState(new PlaybackState()),
                new RecordingDispatcher(),
                CreateMapper(),
                TestLogging.CreateLogger()),
            new ScheduleCommandExecutorUiHooks(
                GetMusicSelectionContainerViewModel: () => null,
                GetAlarmSettingsContainerViewModel: null,
                GetNumberOfTrackContainerViewModel: null,
                SetIsSaving: null,
                SetIsCancelBusy: cancelBusyFlags.Add,
                SetIsSaveBusy: null,
                SetIsDeleteBusy: null));

        sut.InitializeCommands(out var cancel, out _, out _);
        Assert.NotNull(cancel);
        await ((IAsyncRelayCommand)cancel).ExecuteAsync(null);

        var call = Assert.Single(commands.CancelCalls);
        Assert.False(call.IsNew);
        Assert.Equal(42, call.ScheduleId);
        Assert.Equal(new[] { true, false }, cancelBusyFlags);
    }

    [Fact]
    public async Task DeleteCommand_skips_service_when_only_one_saved_schedule_exists()
    {
        var schedule = Schedule(9);
        var appState = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { schedule }, schedule);
        var commands = new RecordingScheduleCommandService();

        var sut = new ScheduleCommandExecutor(
            new ScheduleCommandExecutorCoreDeps(
                commands,
                new NoOpScheduleMediaCacheService(),
                new FakeApplicationState(appState),
                new FakePlaybackState(new PlaybackState()),
                new RecordingDispatcher(),
                CreateMapper(),
                TestLogging.CreateLogger()),
            new ScheduleCommandExecutorUiHooks(
                GetMusicSelectionContainerViewModel: () => null,
                GetAlarmSettingsContainerViewModel: null,
                GetNumberOfTrackContainerViewModel: null,
                SetIsSaving: null,
                SetIsCancelBusy: null,
                SetIsSaveBusy: null,
                SetIsDeleteBusy: null));

        sut.InitializeCommands(out _, out _, out var delete);
        await ((IAsyncRelayCommand)delete!).ExecuteAsync(null);

        Assert.Empty(commands.DeleteCalls);
    }

    [Fact]
    public async Task DeleteCommand_invokes_service_when_multiple_saved_schedules_exist()
    {
        var current = Schedule(2);
        var appState = new ApplicationState(
            new ObservableHashSet<ScheduleStateItem> { Schedule(1), current },
            current);
        var commands = new RecordingScheduleCommandService();

        var sut = new ScheduleCommandExecutor(
            new ScheduleCommandExecutorCoreDeps(
                commands,
                new NoOpScheduleMediaCacheService(),
                new FakeApplicationState(appState),
                new FakePlaybackState(new PlaybackState()),
                new RecordingDispatcher(),
                CreateMapper(),
                TestLogging.CreateLogger()),
            new ScheduleCommandExecutorUiHooks(
                GetMusicSelectionContainerViewModel: () => null,
                GetAlarmSettingsContainerViewModel: null,
                GetNumberOfTrackContainerViewModel: null,
                SetIsSaving: null,
                SetIsCancelBusy: null,
                SetIsSaveBusy: null,
                SetIsDeleteBusy: null));

        sut.InitializeCommands(out _, out _, out var delete);
        await ((IAsyncRelayCommand)delete!).ExecuteAsync(null);

        var call = Assert.Single(commands.DeleteCalls);
        Assert.False(call.IsNew);
        Assert.Equal(2, call.ScheduleId);
        Assert.Equal(2, call.ScheduleCount);
    }

    [Fact]
    public async Task SaveCommand_invokes_save_service_for_current_schedule()
    {
        var current = Schedule(6);
        var appState = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { current }, current);
        var commands = new RecordingScheduleCommandService();

        var sut = new ScheduleCommandExecutor(
            new ScheduleCommandExecutorCoreDeps(
                commands,
                new NoOpScheduleMediaCacheService(),
                new FakeApplicationState(appState),
                new FakePlaybackState(new PlaybackState()),
                new RecordingDispatcher(),
                CreateMapper(),
                TestLogging.CreateLogger()),
            new ScheduleCommandExecutorUiHooks(
                GetMusicSelectionContainerViewModel: () => null,
                GetAlarmSettingsContainerViewModel: null,
                GetNumberOfTrackContainerViewModel: null,
                SetIsSaving: null,
                SetIsCancelBusy: null,
                SetIsSaveBusy: null,
                SetIsDeleteBusy: null));

        sut.InitializeCommands(out _, out var save, out _);
        await ((IAsyncRelayCommand)save!).ExecuteAsync(null);

        var call = Assert.Single(commands.SaveCalls);
        Assert.False(call.IsNew);
        Assert.Equal(6, call.ScheduleId);
    }

    [Fact]
    public async Task SaveCommand_stops_playback_when_bible_publication_changed_for_playing_schedule()
    {
        var persisted = Schedule(5);
        persisted.BiblePublicationLanguageCode = "E";
        persisted.BiblePublicationCode = "nwt";
        persisted.BiblePublicationSectionCode = "1";
        persisted.BiblePublicationTrackCode = "1";
        var current = Schedule(5);
        current.BiblePublicationLanguageCode = "E";
        current.BiblePublicationCode = "nwt";
        current.BiblePublicationSectionCode = "1";
        current.BiblePublicationTrackCode = "2";
        var appState = new ApplicationState(
            new ObservableHashSet<ScheduleStateItem> { persisted },
            current);
        var commands = new RecordingScheduleCommandService();
        var playback = new PlaybackState
        {
            IsPreparingOrPlaying = true,
            CurrentScheduleId = 5,
        };

        var sut = new ScheduleCommandExecutor(
            new ScheduleCommandExecutorCoreDeps(
                commands,
                new NoOpScheduleMediaCacheService(),
                new FakeApplicationState(appState),
                new FakePlaybackState(playback),
                new RecordingDispatcher(),
                CreateMapper(),
                TestLogging.CreateLogger()),
            new ScheduleCommandExecutorUiHooks(
                GetMusicSelectionContainerViewModel: () => null,
                GetAlarmSettingsContainerViewModel: null,
                GetNumberOfTrackContainerViewModel: null,
                SetIsSaving: null,
                SetIsCancelBusy: null,
                SetIsSaveBusy: null,
                SetIsDeleteBusy: null));

        sut.InitializeCommands(out _, out var save, out _);
        await ((IAsyncRelayCommand)save!).ExecuteAsync(null);

        var stop = Assert.Single(commands.StopPlaybackCalls);
        Assert.False(stop.IsNew);
        Assert.True(stop.IsPreparing);
        Assert.Equal(5, stop.ScheduleId);
        Assert.Equal(5, stop.PlaybackScheduleId);
    }
}
