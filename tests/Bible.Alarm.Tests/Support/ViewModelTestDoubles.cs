#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.Interfaces;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests.Support;

internal static class ViewModelTestDoubles
{
    internal sealed class MutableApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value { get; set; } = value;

        public event EventHandler? StateChanged;

        public void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal sealed class MutablePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value { get; set; } = value;

        public event EventHandler? StateChanged;

        public void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    internal sealed class StubPlaybackModalService : IPlaybackModalService
    {
        public bool IsModalOpenOrPending { get; set; }

        public bool IsMinimized { get; set; }

        public void Dispose()
        {
        }

        public Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync() => Task.FromResult(false);

        public Task ShowPlaybackModalIfNeededOnResumeAsync() => Task.CompletedTask;

        public void ShowMiniBarIfPlaybackActiveOnResume()
        {
        }

        public void SubscribeToPlaybackStateChanges()
        {
        }

        public void UnsubscribeToPlaybackStateChanges()
        {
        }

        public bool WasRecentlyMinimized() => false;
    }

    internal sealed class StubScheduleSelectionService : IScheduleSelectionService
    {
        public void Dispose()
        {
        }

        public AlarmMusic? LoadMusicForSelection(LoadMusicForSelectionArgs args) => null;

        public BiblePublicationSchedule? LoadBiblePublicationForSelection(LoadBiblePublicationForSelectionArgs args) => null;
    }

    internal sealed class StubToastService : IToastService
    {
        public void Dispose()
        {
        }

        public Task ShowMessage(string message, int seconds = 3) => Task.CompletedTask;

        public Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3) => Task.CompletedTask;

        public Task Clear() => Task.CompletedTask;
    }

    internal sealed class StubScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new StubScope();

        private sealed class StubScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

            public void Dispose()
            {
            }
        }
    }

    internal sealed class NoopScheduleInitializationService : IScheduleInitializationService
    {
        public Task<ScheduleStateItem> InitializeNewScheduleAsync() => Task.FromResult(new ScheduleStateItem());

        public Task<ScheduleStateItem?> LoadExistingScheduleAsync(
            int scheduleId,
            bool isEnabled,
            ScheduleStateItem? existingFromState = null) =>
            Task.FromResult<ScheduleStateItem?>(null);

        public Task CompleteScheduleLoadAsync() => Task.CompletedTask;

        public void InitializeTrackingFields(
            ScheduleStateItem scheduleStateItem,
            ref int lastScheduleId,
            ref string? lastMusicTrackCode,
            ref string? lastMusicPublicationCode,
            ref string? lastMusicLanguageCode,
            ref bool? lastMusicRepeat)
        {
        }
    }

    internal sealed class NoopScheduleCommandService : IScheduleCommandService
    {
        public Task ExecuteCancelAsync(bool isNewSchedule, int scheduleId, ScheduleStateItem? currentSchedule) =>
            Task.CompletedTask;

        public Task<bool> ExecuteSaveAsync(
            bool isNewSchedule,
            int scheduleId,
            ScheduleStateItem currentSchedule,
            bool musicUpdated,
            bool biblePublicationUpdated,
            bool modelInitialized) => Task.FromResult(true);

        public Task<bool> ExecuteDeleteAsync(bool isNewSchedule, int scheduleId, int scheduleCount) =>
            Task.FromResult(true);

        public Task ValidateNotificationPermissionsAsync(ScheduleStateItem? currentSchedule) => Task.CompletedTask;

        public Task StopPlaybackIfNeededAsync(
            bool isNewSchedule,
            bool isPreparingOrPlaying,
            int scheduleId,
            int currentPlaybackScheduleId) => Task.CompletedTask;

        public Task HandleSaveResultAsync(bool saved, int scheduleId, bool isEnabled, AlarmSchedule model) =>
            Task.CompletedTask;
    }

    internal sealed class NoopScheduleMediaCacheService : IScheduleMediaCacheService
    {
        public void SetupMediaCache(int scheduleId, bool isUpdate = false)
        {
        }
    }

    internal sealed class NoopScheduleContainerService : IScheduleContainerService
    {
        public Task InitializeContainersAsync(
            IServiceProvider serviceProvider,
            Action<BiblePublicationSelectionContainerViewModel, MusicSelectionContainerViewModel, NumberOfTrackContainerViewModel, ScheduleDetailsContainerViewModel, AlarmSettingsContainerViewModel> onContainersReady) =>
            Task.CompletedTask;
    }

    internal sealed class NoopScheduleStateChangeHandler : IScheduleStateChangeHandler
    {
        public bool HandleScheduleUpdateFromState(
            ScheduleStateItem? currentSchedule,
            ref string? lastMusicTrackCode,
            ref string? lastMusicPublicationCode,
            ref string? lastMusicLanguageCode,
            ref bool? lastMusicRepeat,
            out bool hasChanges)
        {
            hasChanges = false;
            return false;
        }

        public void ResetMusicTrackingFields(
            ref string? lastMusicTrackCode,
            ref string? lastMusicPublicationCode,
            ref string? lastMusicLanguageCode,
            ref bool? lastMusicRepeat)
        {
        }

        public void UpdateMusicTrackingFields(
            ScheduleStateItem scheduleStateItem,
            ref string? lastMusicTrackCode,
            ref string? lastMusicPublicationCode,
            ref string? lastMusicLanguageCode,
            ref bool? lastMusicRepeat)
        {
        }
    }

    internal static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    internal static IServiceProvider CreateHomeServiceProvider(IPlaybackModalService? playbackModalService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(playbackModalService ?? new StubPlaybackModalService());
        return services.BuildServiceProvider();
    }

    internal static HomeViewModelDeps CreateHomeDeps(
        RecordingDispatcher? dispatcher = null,
        MutableApplicationState? appState = null,
        MutablePlaybackState? playbackState = null)
    {
        var schedules = new ObservableHashSet<ScheduleStateItem>();
        return new HomeViewModelDeps(
            TestLogging.CreateLogger(),
            CreateHomeServiceProvider(),
            appState ?? new MutableApplicationState(new ApplicationState(schedules, null)),
            playbackState ?? new MutablePlaybackState(new PlaybackState()),
            dispatcher ?? new RecordingDispatcher(),
            new UnusedNavigationServiceStub(),
            CreateMapper());
    }

    internal static ScheduleViewModelDeps CreateScheduleDeps(
        RecordingDispatcher? dispatcher = null,
        MutableApplicationState? appState = null,
        MutablePlaybackState? playbackState = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServiceScopeFactory>(new StubScopeFactory());
        return new ScheduleViewModelDeps(
            TestLogging.CreateLogger(),
            services.BuildServiceProvider(),
            CreateMapper(),
            appState ?? new MutableApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null)),
            playbackState ?? new MutablePlaybackState(new PlaybackState()),
            dispatcher ?? new RecordingDispatcher(),
            new NoopScheduleInitializationService(),
            new NoopScheduleCommandService(),
            new NoopScheduleMediaCacheService(),
            new NoopScheduleContainerService(),
            new NoopScheduleStateChangeHandler());
    }

    internal static MusicSelectionContainerViewModelDeps CreateMusicContainerDeps(
        RecordingDispatcher? dispatcher = null,
        MutableApplicationState? appState = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServiceScopeFactory>(new StubScopeFactory());
        return new MusicSelectionContainerViewModelDeps(
            TestLogging.CreateLogger(),
            new UnusedNavigationServiceStub(),
            new StubScheduleSelectionService(),
            new IdleCatalogMediaService(),
            appState ?? new MutableApplicationState(new ApplicationState { CurrentSchedule = null }),
            dispatcher ?? new RecordingDispatcher(),
            CreateMapper(),
            services.BuildServiceProvider(),
            new StubToastService());
    }
}
