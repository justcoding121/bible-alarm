#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemViewModelTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) => Dispatched.Add(action);
    }
#pragma warning restore CS0067

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

    private sealed class NopSchedulePlaybackService : ISchedulePlaybackService
    {
        public Task<bool> CanMoveTrackAsync(int scheduleId) => Task.FromResult(true);

        public Task PlayScheduleAsync(int scheduleId) => Task.CompletedTask;
    }

    private sealed class NopStopPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession => false;

        public Task PauseAsync() => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PlayNextAsync() => Task.CompletedTask;

        public Task PlayPreviousAsync() => Task.CompletedTask;

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;

        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;

        public Task SeekBackwardAsync() => Task.CompletedTask;

        public Task SeekForwardAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task StopForTeardownAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class NopScheduleStateService : IScheduleStateService
    {
        public void Dispose()
        {
        }

        public Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled) =>
            Task.FromResult(true);
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) =>
            categoryCode switch
            {
                "Bible" => "Bible Reading",
                "Music" => "Music",
                _ => categoryCode,
            };
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleListItemViewModel CreateSut(
        ApplicationState appState,
        IDispatcher? dispatcher = null)
    {
        var deps = new ScheduleListItemViewModelDeps(
            TestLogging.CreateLogger(),
            new NopSchedulePlaybackService(),
            new NopStopPlaybackService(),
            new NopScheduleStateService(),
            new FakeApplicationState(appState),
            new FakePlaybackState(new PlaybackState()),
            (IDispatcher?)dispatcher ?? new NopDispatcher(),
            CreateMapper(),
            new StubCategoryNameService());

        return new ScheduleListItemViewModel(deps);
    }

    private static (AlarmSchedule schedule, ScheduleStateItem stateItem) CreateMorningSchedule(int id = 5)
    {
        var schedule = new AlarmSchedule
        {
            Id = id,
            Name = "Morning",
            Hour = 6,
            Minute = 30,
            DaysOfWeek = WeekDays.Monday,
            IsEnabled = true,
            MusicEnabled = true,
        };

        var stateItem = new ScheduleStateItem
        {
            Id = id,
            Name = "Morning",
            Hour = 6,
            Minute = 30,
            DaysOfWeek = WeekDays.Monday,
            IsEnabled = true,
            MusicEnabled = true,
            BiblePublicationCategoryName = "Bible",
            BiblePublicationLanguageName = "English",
            BiblePublicationName = "New World Translation",
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackTitle = "Chapter 1",
            BiblePublicationTrackCode = "1",
        };

        return (schedule, stateItem);
    }

    [Fact]
    public void Ctor_builds_with_minimal_dependencies()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsNavigating_defaults_to_false()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            Assert.False(sut.IsNavigating);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsNavigating_can_be_set_true()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            sut.IsNavigating = true;

            Assert.True(sut.IsNavigating);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Collection("MauiUi")]
    public sealed class MauiScheduleListItemViewModelTests(MauiUiFixture fixture)
    {
        private static void RunWithMainThreadOrSkip(Action testBody)
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            try
            {
                testBody();
            }
            catch (COMException)
            {
                // Headless xUnit cannot initialize WinUI MainThread; device/CI app hosts cover this path.
            }
        }

        [Fact]
        public void InitializeFromSchedule_exposes_schedule_name()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    var schedules = new ObservableHashSet<ScheduleStateItem> { stateItem };
                    sut = CreateSut(new ApplicationState(schedules, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal("Morning", sut.Name);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void InitializeFromSchedule_exposes_formatted_time_text()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal("06:30", sut.TimeText);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void InitializeFromSchedule_reflects_enabled_state()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.True(sut.IsEnabled);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void CategoryNameDisplay_hides_when_category_equals_schedule_name()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    stateItem.BiblePublicationCategoryName = "Morning";
                    schedule.Name = "Morning";
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal(string.Empty, sut.CategoryNameDisplay);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void MusicEnabled_reflects_schedule_flag()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.True(sut.MusicEnabled);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void DeleteCommand_dispatches_delete_action_when_multiple_schedules_exist()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var dispatcher = new RecordingDispatcher();
                    var first = new ScheduleStateItem { Id = 1, Name = "A" };
                    var second = new ScheduleStateItem { Id = 2, Name = "B" };
                    var (schedule, stateItem) = CreateMorningSchedule(2);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { first, second }, null), dispatcher);
                    sut.InitializeFromSchedule(schedule, stateItem);

                    sut.DeleteCommand.Execute(null);

                    Assert.Contains(dispatcher.Dispatched, action => action is DeleteScheduleAction delete && delete.ScheduleId == 2);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void ToggleEnabledCommand_toggles_is_enabled()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    schedule.DaysOfWeek = WeekDays.Monday;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    sut.ToggleEnabledCommand.Execute(null);

                    Assert.False(sut.IsEnabled);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }
    }
}
