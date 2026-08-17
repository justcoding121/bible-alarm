#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class WindowsToastNotificationUniqueIdComposerBibleAlarmTests
{
    [Fact]
    public void Compose_keeps_full_suffix_when_under_platform_length_ceiling()
    {
        var fireDate = new DateTimeOffset(2028, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var id = WindowsToastNotificationUniqueIdComposer.Compose(12, fireDate);

        Assert.True(id.Length <= 16);
        Assert.StartsWith("12_", id, StringComparison.Ordinal);
        Assert.Equal("12_".Length + Sha256Base36CompactHasher.To11CharacterToken(fireDate).Length, id.Length);
    }

    [Fact]
    public void Compose_truncates_hash_when_schedule_digits_leave_no_room_for_full_suffix()
    {
        var fireDate = new DateTimeOffset(2031, 9, 8, 7, 6, 5, TimeSpan.Zero);

        var id = WindowsToastNotificationUniqueIdComposer.Compose(999_999_999, fireDate);

        Assert.Equal(16, id.Length);
        Assert.StartsWith("999999999_", id, StringComparison.Ordinal);
    }
}

public sealed class ToastVisibilityDelayMillisecondsBibleAlarmTests
{
    [Fact]
    public void FromNonNegativeUiMilliseconds_clamps_negative_values_to_zero()
    {
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromNonNegativeUiMilliseconds(-5));
    }

    [Fact]
    public void FromToastDurationSeconds_returns_zero_for_non_positive_or_non_finite_inputs()
    {
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(-1));
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(double.NaN));
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(double.PositiveInfinity));
    }

    [Fact]
    public void FromToastDurationSeconds_caps_scaled_milliseconds_at_int_max_value()
    {
        Assert.Equal(int.MaxValue, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds((double)int.MaxValue));
    }

    [Fact]
    public void FromToastDurationSeconds_truncates_scaled_fractional_seconds_toward_zero()
    {
        Assert.Equal(1500, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(1.5));
    }
}

public sealed class EventToCommandParameterResolverBibleAlarmTests
{
    private sealed class SampleArgs : EventArgs;

    [Fact]
    public void Resolve_keeps_explicit_command_parameter_even_when_event_args_are_present()
    {
        var ea = new SampleArgs();
        var resolved = EventToCommandParameterResolver.Resolve(
            commandParameter: "bound-parameter",
            eventArgs: ea,
            convertEventArgs: (_, _, _) => "converted-should-not-run",
            converterParameter: null,
            culture: CultureInfo.InvariantCulture);

        Assert.Equal("bound-parameter", resolved);
    }

    [Fact]
    public void Resolve_falls_back_to_event_args_when_command_parameter_null_and_args_are_meaningful()
    {
        var ea = new SampleArgs();
        var resolved = EventToCommandParameterResolver.Resolve(null, ea, convertEventArgs: null, converterParameter: null,
            CultureInfo.InvariantCulture);

        Assert.Same(ea, resolved);
    }

    [Fact]
    public void Resolve_leaves_parameter_null_when_event_args_are_EventArgs_Empty()
    {
        var resolved = EventToCommandParameterResolver.Resolve(null, EventArgs.Empty, convertEventArgs: null,
            converterParameter: null,
            CultureInfo.InvariantCulture);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_applies_converter_when_binding_parameter_from_non_empty_event_args()
    {
        var ea = new SampleArgs();
        var resolved = EventToCommandParameterResolver.Resolve(
            null,
            ea,
            (_, _, _) => 42,
            converterParameter: null,
            CultureInfo.InvariantCulture);

        Assert.Equal(42, resolved);
    }
}

public sealed class MediaDbContextFactoryBibleAlarmTests
{
    [Fact]
    public void CreateDbContext_uses_sqlite_connection_with_media_index_file()
    {
        var sut = new MediaDbContextFactory();

        using var context = sut.CreateDbContext([]);
        var connectionString = context.Database.GetDbConnection().ConnectionString;

        Assert.NotNull(connectionString);
        Assert.Contains(AppConstants.Database.MediaIndexDatabaseFileName, connectionString, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ScheduleViewModelDepsBibleAlarmTests
{
    private sealed class MutableState<T>(T value) : IState<T> where T : class
    {
        public T Value { get; set; } = value;
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public void Dispatch(object action) { }
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private sealed class NoopScheduleInitialization : IScheduleInitializationService
    {
        public Task<ScheduleStateItem> InitializeNewScheduleAsync() => Task.FromResult(new ScheduleStateItem());

        public Task<ScheduleStateItem?> LoadExistingScheduleAsync(int scheduleId, bool isEnabled, ScheduleStateItem? existingFromState = null) =>
            Task.FromResult<ScheduleStateItem?>(null);

        public Task CompleteScheduleLoadAsync() => Task.CompletedTask;
    }

    private sealed class NoopScheduleCommand : IScheduleCommandService
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

    private sealed class NoopScheduleMediaCache : IScheduleMediaCacheService
    {
        public void SetupMediaCache(int scheduleId, bool isUpdate = false) { }
    }

    private sealed class NoopScheduleContainer : IScheduleContainerService
    {
        public Task InitializeContainersAsync(
            IServiceProvider serviceProvider,
            Action<BiblePublicationSelectionContainerViewModel, MusicSelectionContainerViewModel, NumberOfTrackContainerViewModel, ScheduleDetailsContainerViewModel, AlarmSettingsContainerViewModel> onContainersReady) =>
            Task.CompletedTask;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public void Record_stores_all_dependencies()
    {
        var logger = TestLogging.CreateLogger();
        var sp = new ServiceCollection().BuildServiceProvider();
        var app = new MutableState<ApplicationState>(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()));
        var playback = new MutableState<PlaybackState>(new PlaybackState());
        var dispatcher = new RecordingDispatcher();
        var mapper = CreateMapper();
        var init = new NoopScheduleInitialization();
        var command = new NoopScheduleCommand();
        var cache = new NoopScheduleMediaCache();
        var container = new NoopScheduleContainer();

        var deps = new ScheduleViewModelDeps(
            logger,
            sp,
            mapper,
            app,
            playback,
            dispatcher,
            init,
            command,
            cache,
            container);

        Assert.Same(logger, deps.Logger);
        Assert.Same(sp, deps.ServiceProvider);
        Assert.Same(mapper, deps.Mapper);
        Assert.Same(app, deps.ApplicationState);
        Assert.Same(playback, deps.PlaybackState);
        Assert.Same(dispatcher, deps.Dispatcher);
        Assert.Same(init, deps.ScheduleInitializationService);
        Assert.Same(command, deps.ScheduleCommandService);
        Assert.Same(cache, deps.ScheduleMediaCacheService);
        Assert.Same(container, deps.ScheduleContainerService);
    }
}
