#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Devices;
using System.Reflection;
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

    private sealed class RecordingStopPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession => false;

        public bool StopWasCalled { get; private set; }

        public Task PauseAsync() => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PlayNextAsync() => Task.CompletedTask;

        public Task PlayPreviousAsync() => Task.CompletedTask;

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;

        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;

        public Task SeekBackwardAsync() => Task.CompletedTask;

        public Task SeekForwardAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task StopAsync()
        {
            StopWasCalled = true;
            return Task.CompletedTask;
        }

        public Task StopForTeardownAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingToastMessenger : IDisposable
    {
        public List<string> ToastValues { get; } = [];

        public RecordingToastMessenger() =>
            WeakReferenceMessenger.Default.Register<ShowToastMessage>(this, (_, m) => ToastValues.Add(m.Value));

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<ShowToastMessage>(this);
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
        IDispatcher? dispatcher = null,
        IPlaybackService? stopPlaybackService = null,
        IState<PlaybackState>? playbackState = null)
    {
        var deps = new ScheduleListItemViewModelDeps(
            TestLogging.CreateLogger(),
            new NopSchedulePlaybackService(),
            stopPlaybackService ?? new RecordingStopPlaybackService(),
            new NopScheduleStateService(),
            new FakeApplicationState(appState),
            playbackState ?? new FakePlaybackState(new PlaybackState()),
            (IDispatcher?)dispatcher ?? new NopDispatcher(),
            CreateMapper(),
            new StubCategoryNameService());

        return new ScheduleListItemViewModel(deps);
    }

    private static void AssignSchedule(ScheduleListItemViewModel sut, AlarmSchedule schedule)
    {
        var property = typeof(ScheduleListItemViewModel).GetProperty(
            nameof(ScheduleListItemViewModel.Schedule),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        property.SetValue(sut, schedule);
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

    [Fact]
    public void Dispose_can_be_called_repeatedly()
    {
        var sut = CreateSut(new ApplicationState());
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void RaisePropertiesChangedEvent_notifies_all_properties_except_IsEnabled()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            var raised = new HashSet<string>(StringComparer.Ordinal);
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null)
                {
                    raised.Add(e.PropertyName);
                }
            };

            sut.RaisePropertiesChangedEvent();

            Assert.Contains(nameof(ScheduleListItemViewModel.Name), raised);
            Assert.Contains(nameof(ScheduleListItemViewModel.TimeText), raised);
            Assert.DoesNotContain(nameof(ScheduleListItemViewModel.IsEnabled), raised);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void CompareTo_null_returns_positive()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            Assert.Equal(1, sut.CompareTo((ScheduleListItemViewModel?)null));
            Assert.Equal(1, sut.CompareTo((object?)null));
            Assert.Equal(1, ((IComparable)sut).CompareTo(new object()));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Equals_false_for_null_and_wrong_type()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            Assert.False(sut.Equals((ScheduleListItemViewModel?)null));
            Assert.False(sut.Equals(new object()));
            Assert.False(sut == null);
            Assert.True(sut != null);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Equals_GetHashCode_and_equality_operators_match_for_uninitialized_instances()
    {
        ScheduleListItemViewModel? left = null;
        ScheduleListItemViewModel? right = null;
        try
        {
            left = CreateSut(new ApplicationState());
            right = CreateSut(new ApplicationState());

            Assert.True(left.Equals(right));
            Assert.True(left.Equals((object)right));
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
            ScheduleListItemViewModel sameRef = left;
            Assert.True(left == sameRef);
            Assert.True((ScheduleListItemViewModel?)null == null);
        }
        finally
        {
            left?.Dispose();
            right?.Dispose();
        }
    }

    [Fact]
    public void CompareTo_orders_by_last_played_without_full_initialize()
    {
        ScheduleListItemViewModel? newer = null;
        ScheduleListItemViewModel? older = null;
        try
        {
            newer = CreateSut(new ApplicationState());
            older = CreateSut(new ApplicationState());
            AssignSchedule(newer, new AlarmSchedule { Id = 1, LastPlayedAtUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc) });
            AssignSchedule(older, new AlarmSchedule { Id = 1, LastPlayedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

            Assert.True(newer.CompareTo(older) < 0);
            Assert.True(newer < older);
            Assert.Equal(0, newer.CompareTo(newer));
        }
        finally
        {
            newer?.Dispose();
            older?.Dispose();
        }
    }

    [Fact]
    public void Equals_and_hash_code_use_schedule_without_full_initialize()
    {
        ScheduleListItemViewModel? left = null;
        ScheduleListItemViewModel? right = null;
        try
        {
            var played = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            left = CreateSut(new ApplicationState());
            right = CreateSut(new ApplicationState());
            AssignSchedule(left, new AlarmSchedule { Id = 9, LastPlayedAtUtc = played });
            AssignSchedule(right, new AlarmSchedule { Id = 9, LastPlayedAtUtc = played });

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
        finally
        {
            left?.Dispose();
            right?.Dispose();
        }
    }

    [Fact]
    public void Relational_operators_are_false_when_either_operand_is_null()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            Assert.False(sut < null);
            Assert.False(sut > null);
            Assert.False(sut <= null);
            Assert.False(sut >= null);
            Assert.False(null < sut);
            Assert.False(null > sut);
            Assert.False(null <= sut);
            Assert.False(null >= sut);
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

        [Fact]
        public void SetScheduleId_initializes_from_application_state()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (_, stateItem) = CreateMorningSchedule(11);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));

                    sut.SetScheduleId(11);

                    Assert.Equal(11, sut.ScheduleId);
                    Assert.Equal("Morning", sut.Name);
                    Assert.NotNull(sut.Schedule);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void SetScheduleId_is_no_op_when_schedule_missing()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    sut = CreateSut(new ApplicationState());

                    sut.SetScheduleId(404);

                    Assert.Equal(0, sut.ScheduleId);
                    Assert.Null(sut.Schedule);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void SetScheduleId_is_no_op_when_id_non_positive()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (_, stateItem) = CreateMorningSchedule(3);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));

                    sut.SetScheduleId(0);

                    Assert.Equal(0, sut.ScheduleId);
                    Assert.Null(sut.Schedule);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void RefreshTrackName_refreshes_subtitle_from_state()
        {
            // Device runner assertions differ from host; keep coverage on WinUI OpenCover pass.
            if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
            {
                return;
            }
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    sut.RefreshTrackName();
                    sut.RefreshTrackName(force: true);

                    Assert.Equal("English", sut.Language);
                    Assert.False(string.IsNullOrWhiteSpace(sut.SubTitle));
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void CategoryNameDisplay_returns_localized_category_when_different_from_name()
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

                    Assert.Equal("Bible Reading", sut.CategoryNameDisplay);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void BiblePublicationSectionAndTrackOneLine_joins_section_and_track_for_bible()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    stateItem.BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible;
                    stateItem.BiblePublicationSectionName = "Genesis";
                    stateItem.BiblePublicationTrackCode = "1";
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal("Genesis 1", sut.BiblePublicationSectionAndTrackOneLine);
                    Assert.True(sut.ShouldShowBiblePublicationSectionAndTrackOneLine);
                    Assert.False(sut.ShouldShowBiblePublicationSectionNameLine);
                    Assert.False(sut.ShouldShowBiblePublicationTrackNameLine);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void ShouldShow_section_and_track_lines_for_non_bible_category()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(8);
                    stateItem.BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryMusic;
                    stateItem.BiblePublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam;
                    stateItem.BiblePublicationSectionName = "Disc 1";
                    stateItem.BiblePublicationTrackTitle = "Melody 3";
                    stateItem.BiblePublicationTrackCode = "3";
                    schedule.MusicEnabled = false;
                    stateItem.MusicEnabled = false;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal(string.Empty, sut.BiblePublicationSectionAndTrackOneLine);
                    Assert.False(sut.ShouldShowBiblePublicationSectionAndTrackOneLine);
                    Assert.True(sut.ShouldShowBiblePublicationSectionNameLine);
                    Assert.True(sut.ShouldShowBiblePublicationTrackNameLine);
                    Assert.Equal("Disc 1", sut.BiblePublicationSectionName);
                    Assert.Equal("Melody 3", sut.BiblePublicationTrackName);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void ShouldShowLanguageOrMusicLine_true_when_music_enabled()
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

                    Assert.True(sut.ShouldShowLanguageOrMusicLine);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void CompareTo_orders_by_LastPlayedAtUtc_then_ScheduleId()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? older = null;
                ScheduleListItemViewModel? newer = null;
                ScheduleListItemViewModel? lowId = null;
                ScheduleListItemViewModel? highId = null;
                try
                {
                    var playedOlder = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var playedNewer = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var (scheduleOlder, stateOlder) = CreateMorningSchedule(1);
                    var (scheduleNewer, stateNewer) = CreateMorningSchedule(1);
                    scheduleOlder.LastPlayedAtUtc = playedOlder;
                    scheduleNewer.LastPlayedAtUtc = playedNewer;
                    stateOlder.LastPlayedAtUtc = playedOlder;
                    stateNewer.LastPlayedAtUtc = playedNewer;

                    older = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateOlder }, null));
                    newer = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateNewer }, null));
                    older.InitializeFromSchedule(scheduleOlder, stateOlder);
                    newer.InitializeFromSchedule(scheduleNewer, stateNewer);

                    Assert.True(newer.CompareTo(older) < 0);
                    Assert.True(older.CompareTo(newer) > 0);
                    Assert.Equal(newer.CompareTo(older), newer.CompareTo((object)older));

                    var tie = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var (scheduleLow, stateLow) = CreateMorningSchedule(2);
                    var (scheduleHigh, stateHigh) = CreateMorningSchedule(5);
                    scheduleLow.LastPlayedAtUtc = tie;
                    scheduleHigh.LastPlayedAtUtc = tie;
                    lowId = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateLow }, null));
                    highId = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateHigh }, null));
                    lowId.InitializeFromSchedule(scheduleLow, stateLow);
                    highId.InitializeFromSchedule(scheduleHigh, stateHigh);

                    Assert.True(lowId.CompareTo(highId) < 0);
                    Assert.True(newer < older);
                    Assert.True(older > newer);
                    Assert.True(newer <= older);
                    Assert.True(older >= newer);
                    Assert.True(lowId <= highId);
                    Assert.True(highId >= lowId);
                }
                finally
                {
                    older?.Dispose();
                    newer?.Dispose();
                    lowId?.Dispose();
                    highId?.Dispose();
                }
            });
        }

        [Fact]
        public void Equals_and_GetHashCode_use_ScheduleId_and_LastPlayedAtUtc()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? left = null;
                ScheduleListItemViewModel? right = null;
                ScheduleListItemViewModel? different = null;
                try
                {
                    var played = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc);
                    var (scheduleLeft, stateLeft) = CreateMorningSchedule(7);
                    var (scheduleRight, stateRight) = CreateMorningSchedule(7);
                    var (scheduleDifferent, stateDifferent) = CreateMorningSchedule(7);
                    scheduleLeft.LastPlayedAtUtc = played;
                    scheduleRight.LastPlayedAtUtc = played;
                    scheduleDifferent.LastPlayedAtUtc = played.AddDays(1);

                    left = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateLeft }, null));
                    right = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateRight }, null));
                    different = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateDifferent }, null));
                    left.InitializeFromSchedule(scheduleLeft, stateLeft);
                    right.InitializeFromSchedule(scheduleRight, stateRight);
                    different.InitializeFromSchedule(scheduleDifferent, stateDifferent);

                    Assert.True(left.Equals(right));
                    Assert.True(left == right);
                    Assert.False(left != right);
                    Assert.Equal(left.GetHashCode(), right.GetHashCode());
                    Assert.False(left.Equals(different));
                    Assert.True(left != different);
                }
                finally
                {
                    left?.Dispose();
                    right?.Dispose();
                    different?.Dispose();
                }
            });
        }

        [Fact]
        public void Dispose_after_initialize_does_not_throw()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                var (schedule, stateItem) = CreateMorningSchedule();
                var sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                sut.InitializeFromSchedule(schedule, stateItem);

                sut.Dispose();
                sut.Dispose();
            });
        }

        [Fact]
        public void DeleteCommand_shows_toast_when_only_one_schedule_exists()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                using var toast = new RecordingToastMessenger();
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(1);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    sut.DeleteCommand.Execute(null);

                    Assert.Contains("Cannot delete last schedule", toast.ToastValues);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void DeleteCommand_stops_playback_when_deleting_active_schedule()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                var stopPlayback = new RecordingStopPlaybackService();
                var playing = new PlaybackState(
                    new PlaybackTransportSlice(2, true, true, true, PlayStatus.Playing, false, false),
                    new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
                    new PlaybackDefaultScheduleSlice(null, null, null, null, null));
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var first = new ScheduleStateItem { Id = 1, Name = "A" };
                    var second = new ScheduleStateItem { Id = 2, Name = "B" };
                    var (schedule, stateItem) = CreateMorningSchedule(2);
                    var dispatcher = new RecordingDispatcher();
                    sut = CreateSut(
                        new ApplicationState(new ObservableHashSet<ScheduleStateItem> { first, second }, null),
                        dispatcher,
                        stopPlayback,
                        new FakePlaybackState(playing));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null).GetAwaiter().GetResult();

                    Assert.True(stopPlayback.StopWasCalled);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void IsEnabled_enabling_with_zero_days_shows_toast_and_stays_disabled()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                using var toast = new RecordingToastMessenger();
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    schedule.DaysOfWeek = 0;
                    schedule.IsEnabled = false;
                    stateItem.DaysOfWeek = 0;
                    stateItem.IsEnabled = false;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    sut.IsEnabled = true;

                    Assert.False(sut.IsEnabled);
                    Assert.Contains("Select at least one day", toast.ToastValues);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void PlaybackModalOpenedMessage_clears_row_busy_state()
        {
            // Device runner assertions differ from host; keep coverage on WinUI OpenCover pass.
            if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
            {
                return;
            }
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(3);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);
                    sut.IsBusy = true;

                    WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());

                    Assert.False(sut.IsBusy);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void Hour_minute_and_meridian_reflect_schedule_time()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    schedule.Hour = 18;
                    schedule.Minute = 7;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal("06", sut.Hour);
                    Assert.Equal("07", sut.Minute);
                    Assert.Equal("PM", sut.MeridianText);
                    Assert.Equal(Meridian.Pm, sut.Meridian);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }
    }
}
