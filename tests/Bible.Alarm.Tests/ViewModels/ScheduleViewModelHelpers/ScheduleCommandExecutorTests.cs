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

        public Task ExecuteCancelAsync(bool isNewSchedule, int scheduleId, ScheduleStateItem? currentSchedule)
        {
            CancelCalls.Add((isNewSchedule, scheduleId));
            return Task.CompletedTask;
        }

        public Task<bool> ExecuteSaveAsync(bool isNewSchedule, int scheduleId, ScheduleStateItem currentSchedule, bool musicUpdated, bool biblePublicationUpdated, bool modelInitialized) =>
            Task.FromResult(false);

        public Task<bool> ExecuteDeleteAsync(bool isNewSchedule, int scheduleId, int scheduleCount) =>
            Task.FromResult(false);

        public Task ValidateNotificationPermissionsAsync(ScheduleStateItem? currentSchedule) =>
            Task.CompletedTask;

        public Task StopPlaybackIfNeededAsync(bool isNewSchedule, bool isPreparingOrPlaying, int scheduleId, int currentPlaybackScheduleId) =>
            Task.CompletedTask;

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
}
