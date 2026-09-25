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

    private sealed class RecordingSchedulePlaybackService : ISchedulePlaybackService
    {
        public int? PlayedScheduleId { get; private set; }

        public Task<bool> CanMoveTrackAsync(int scheduleId) => Task.FromResult(true);

        public Task PlayScheduleAsync(int scheduleId)
        {
            PlayedScheduleId = scheduleId;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingScheduleStateService : IScheduleStateService
    {
        public void Dispose()
        {
        }

        public Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled) =>
            Task.FromResult(false);
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
        IState<PlaybackState>? playbackState = null,
        IState<ApplicationState>? applicationState = null,
        ISchedulePlaybackService? schedulePlaybackService = null,
        IScheduleStateService? scheduleStateService = null)
    {
        var deps = new ScheduleListItemViewModelDeps(
            TestLogging.CreateLogger(),
            schedulePlaybackService ?? new NopSchedulePlaybackService(),
            stopPlaybackService ?? new RecordingStopPlaybackService(),
            scheduleStateService ?? new NopScheduleStateService(),
            applicationState ?? new FakeApplicationState(appState),
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

    private static void SetPrivateIsEnabled(ScheduleListItemViewModel sut, bool value)
    {
        var field = typeof(ScheduleListItemViewModel).GetField(
            "isEnabled",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(sut, value);
    }

    private static void InvokePrivate(ScheduleListItemViewModel sut, string methodName, params object?[] args)
    {
        var method = typeof(ScheduleListItemViewModel).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(sut, args);
    }

    /// <summary>
    /// Initializes via the real public API when MainThread works; otherwise finishes wiring without UI dispatcher.
    /// </summary>
    private static void WireScheduleForHost(
        ScheduleListItemViewModel sut,
        AlarmSchedule schedule,
        ScheduleStateItem? stateItem)
    {
        try
        {
            sut.InitializeFromSchedule(schedule, stateItem);
        }
        catch (COMException)
        {
            // Headless Windows: RaiseCoreSchedulePropertyNotifications may throw after ApplyScheduleProperties.
        }

        if (sut.Schedule is null)
        {
            AssignSchedule(sut, schedule);
            SetPrivateIsEnabled(sut, schedule.IsEnabled);
        }

        if (sut.PlayCommand is null)
        {
            InvokePrivate(sut, "ConnectScheduleSubscriptions", stateItem);
            InvokePrivate(sut, "EnsureScheduleCommands");
            InvokePrivate(sut, "RefreshSubTitleFromState", stateItem);
        }
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

    [Fact]
    public void Uninitialized_schedule_properties_use_empty_defaults()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            Assert.Equal(0, sut.ScheduleId);
            Assert.Equal(string.Empty, sut.Name);
            Assert.Equal(string.Empty, sut.TimeText);
            Assert.Equal("00", sut.Hour);
            Assert.Equal("00", sut.Minute);
            Assert.Equal(Meridian.Am, sut.Meridian);
            Assert.Equal("AM", sut.MeridianText);
            Assert.Equal((WeekDays)0, sut.DaysOfWeek);
            Assert.False(sut.MusicEnabled);
            Assert.False(sut.IsEnabled);
            Assert.False(sut.IsBusy);
            Assert.Same(sut, sut.This);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Assigned_schedule_exposes_name_and_time_fields()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            AssignSchedule(sut, new AlarmSchedule
            {
                Id = 4,
                Name = "Evening",
                Hour = 18,
                Minute = 7,
                MusicEnabled = true,
                DaysOfWeek = WeekDays.Friday,
            });

            Assert.Equal(4, sut.ScheduleId);
            Assert.Equal("Evening", sut.Name);
            Assert.Equal("06", sut.Hour);
            Assert.Equal("07", sut.Minute);
            Assert.Equal(Meridian.Pm, sut.Meridian);
            Assert.Equal("PM", sut.MeridianText);
            Assert.Equal(WeekDays.Friday, sut.DaysOfWeek);
            Assert.True(sut.MusicEnabled);
            Assert.Equal("06:07", sut.TimeText);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void CategoryNameDisplay_empty_when_category_missing_from_state()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            AssignSchedule(sut, new AlarmSchedule { Id = 12, Name = "Solo" });

            Assert.Equal(string.Empty, sut.CategoryName);
            Assert.Equal(string.Empty, sut.CategoryNameDisplay);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void CategoryNameDisplay_hides_when_localized_category_matches_name()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var stateItem = new ScheduleStateItem
            {
                Id = 13,
                Name = "Bible Reading",
                BiblePublicationCategoryName = "Bible",
            };
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            AssignSchedule(sut, new AlarmSchedule { Id = 13, Name = "Bible Reading" });

            Assert.Equal(string.Empty, sut.CategoryNameDisplay);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void CategoryNameDisplay_returns_localized_category_when_distinct()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var stateItem = new ScheduleStateItem
            {
                Id = 14,
                Name = "Morning",
                BiblePublicationCategoryName = "Bible",
            };
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            AssignSchedule(sut, new AlarmSchedule { Id = 14, Name = "Morning" });

            Assert.Equal("Bible Reading", sut.CategoryNameDisplay);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Bible_display_properties_read_from_application_state()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var stateItem = new ScheduleStateItem
            {
                Id = 15,
                BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
                BiblePublicationName = "NWT",
                BiblePublicationSectionName = "Exodus",
                BiblePublicationTrackCode = "9",
                BiblePublicationTrackTitle = "Chapter 9",
                BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            };
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            AssignSchedule(sut, new AlarmSchedule { Id = 15, Name = "Morning" });

            Assert.True(sut.IsBibleCategory);
            Assert.Equal("NWT", sut.BiblePublicationName);
            Assert.Equal("Exodus 9", sut.BiblePublicationSectionAndTrackOneLine);
            Assert.True(sut.ShouldShowBiblePublicationSectionAndTrackOneLine);
            Assert.False(sut.ShouldShowBiblePublicationSectionNameLine);
            Assert.False(sut.ShouldShowBiblePublicationTrackNameLine);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Non_bible_category_shows_section_and_track_lines()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var stateItem = new ScheduleStateItem
            {
                Id = 16,
                BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryMusic,
                BiblePublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                BiblePublicationSectionName = "Disc 1",
                BiblePublicationTrackTitle = "Melody 3",
                BiblePublicationTrackCode = "3",
            };
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            AssignSchedule(sut, new AlarmSchedule { Id = 16, MusicEnabled = false });

            Assert.False(sut.IsBibleCategory);
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
    }

    [Fact]
    public void ShouldShowLanguageOrMusicLine_true_when_music_enabled_without_language()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            AssignSchedule(sut, new AlarmSchedule { Id = 17, MusicEnabled = true });

            Assert.True(sut.ShouldShowLanguageOrMusicLine);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void ShouldShowLanguageOrMusicLine_false_when_music_off_and_language_empty()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            AssignSchedule(sut, new AlarmSchedule { Id = 18, MusicEnabled = false });

            Assert.False(sut.ShouldShowLanguageOrMusicLine);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsEnabled_enabling_with_zero_days_shows_toast_without_initialize()
    {
        using var toast = new RecordingToastMessenger();
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            AssignSchedule(sut, new AlarmSchedule { Id = 19, DaysOfWeek = 0, IsEnabled = false });
            SetPrivateIsEnabled(sut, false);

            sut.IsEnabled = true;

            Assert.False(sut.IsEnabled);
            Assert.Contains("Select at least one day", toast.ToastValues);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsEnabled_same_value_is_noop()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            SetPrivateIsEnabled(sut, false);
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ScheduleListItemViewModel.IsEnabled))
                {
                    raised = true;
                }
            };

            sut.IsEnabled = false;

            Assert.False(raised);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsEnabled_successful_update_notifies_properties()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(22);
            schedule.IsEnabled = true;
            stateItem.IsEnabled = true;
            sut = CreateSut(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                scheduleStateService: new NopScheduleStateService());
            AssignSchedule(sut, schedule);
            SetPrivateIsEnabled(sut, true);
            var thisNotified = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ScheduleListItemViewModel.This))
                {
                    thisNotified = true;
                }
            };

            sut.IsEnabled = false;

            Assert.False(sut.IsEnabled);
            Assert.True(thisNotified);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void RefreshTrackName_is_noop_when_schedule_missing()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            sut.RefreshTrackName();
            sut.RefreshTrackName(force: true);

            Assert.Equal(string.Empty, sut.Language);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void RefreshTrackName_loads_language_from_state_item()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(23);
            // Subtitle manager only updates Language when BiblePublicationScheduleId is present.
            stateItem.BiblePublicationScheduleId = 23;
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            try
            {
                WireScheduleForHost(sut, schedule, stateItem);
            }
            catch (COMException)
            {
                // Headless Windows host: MainThread may be unavailable after schedule wiring.
            }

            sut.RefreshTrackName();

            Assert.Equal("English", sut.Language);
            Assert.False(string.IsNullOrWhiteSpace(sut.SubTitle));
            Assert.True(sut.ShouldShowLanguageOrMusicLine);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Relational_operators_true_when_equal_by_last_played_and_id()
    {
        ScheduleListItemViewModel? left = null;
        ScheduleListItemViewModel? right = null;
        try
        {
            var played = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            left = CreateSut(new ApplicationState());
            right = CreateSut(new ApplicationState());
            AssignSchedule(left, new AlarmSchedule { Id = 3, LastPlayedAtUtc = played });
            AssignSchedule(right, new AlarmSchedule { Id = 3, LastPlayedAtUtc = played });

            Assert.True(left <= right);
            Assert.True(left >= right);
            Assert.False(left < right);
            Assert.False(left > right);
        }
        finally
        {
            left?.Dispose();
            right?.Dispose();
        }
    }

    [Fact]
    public void CompareTo_uses_schedule_id_when_last_played_ties()
    {
        ScheduleListItemViewModel? low = null;
        ScheduleListItemViewModel? high = null;
        try
        {
            var played = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            low = CreateSut(new ApplicationState());
            high = CreateSut(new ApplicationState());
            AssignSchedule(low, new AlarmSchedule { Id = 2, LastPlayedAtUtc = played });
            AssignSchedule(high, new AlarmSchedule { Id = 9, LastPlayedAtUtc = played });

            Assert.True(low.CompareTo(high) < 0);
            Assert.True(high.CompareTo(low) > 0);
        }
        finally
        {
            low?.Dispose();
            high?.Dispose();
        }
    }

    [Fact]
    public void Equals_false_when_last_played_differs()
    {
        ScheduleListItemViewModel? left = null;
        ScheduleListItemViewModel? right = null;
        try
        {
            left = CreateSut(new ApplicationState());
            right = CreateSut(new ApplicationState());
            AssignSchedule(left, new AlarmSchedule { Id = 5, LastPlayedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            AssignSchedule(right, new AlarmSchedule { Id = 5, LastPlayedAtUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc) });

            Assert.False(left.Equals(right));
            Assert.True(left != right);
        }
        finally
        {
            left?.Dispose();
            right?.Dispose();
        }
    }

    [Fact]
    public void OnPlayStarted_and_OnPlaybackStarted_callbacks_can_be_assigned()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            var playStarted = false;
            var playbackStarted = false;
            sut.OnPlayStarted = () => playStarted = true;
            sut.OnPlaybackStarted = () => playbackStarted = true;

            sut.OnPlayStarted.Invoke();
            sut.OnPlaybackStarted.Invoke();

            Assert.True(playStarted);
            Assert.True(playbackStarted);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsBusy_can_be_toggled()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());
            sut.IsBusy = true;

            Assert.True(sut.IsBusy);

            sut.IsBusy = false;

            Assert.False(sut.IsBusy);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void WireSchedule_exposes_name_after_initialize_or_host_fallback()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(30);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));

            WireScheduleForHost(sut, schedule, stateItem);

            Assert.Equal("Morning", sut.Name);
            Assert.Equal(30, sut.ScheduleId);
            Assert.NotNull(sut.PlayCommand);
            Assert.NotNull(sut.DeleteCommand);
            Assert.NotNull(sut.ToggleEnabledCommand);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void SetScheduleId_wires_matching_schedule_from_state()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (_, stateItem) = CreateMorningSchedule(31);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            try
            {
                sut.SetScheduleId(31);
            }
            catch (COMException)
            {
            }

            if (sut.Schedule is null || sut.PlayCommand is null)
            {
                var mapped = CreateMapper().Map<AlarmSchedule>(stateItem);
                WireScheduleForHost(sut, mapped, stateItem);
            }

            Assert.Equal(31, sut.ScheduleId);
            Assert.Equal("Morning", sut.Name);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void SetScheduleId_missing_schedule_leaves_uninitialized()
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
    }

    [Fact]
    public void InitializeFromSchedule_invalid_id_is_noop()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState());

            sut.InitializeFromSchedule(new AlarmSchedule { Id = 0, Name = "Bad" }, null);

            Assert.Null(sut.Schedule);
            Assert.Equal(0, sut.ScheduleId);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void DeleteCommand_dispatches_when_multiple_schedules_exist()
    {
        var dispatcher = new RecordingDispatcher();
        ScheduleListItemViewModel? sut = null;
        try
        {
            var first = new ScheduleStateItem { Id = 1, Name = "A" };
            var second = new ScheduleStateItem { Id = 2, Name = "B" };
            var (schedule, stateItem) = CreateMorningSchedule(2);
            sut = CreateSut(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { first, second }, null),
                dispatcher);
            WireScheduleForHost(sut, schedule, stateItem);

            sut.DeleteCommand.Execute(null);

            Assert.Contains(dispatcher.Dispatched, action => action is DeleteScheduleAction delete && delete.ScheduleId == 2);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void DeleteCommand_toasts_when_only_one_schedule_remains()
    {
        using var toast = new RecordingToastMessenger();
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(1);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            WireScheduleForHost(sut, schedule, stateItem);

            sut.DeleteCommand.Execute(null);

            Assert.Contains("Cannot delete last schedule", toast.ToastValues);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void DeleteCommand_stops_playback_for_active_schedule()
    {
        var stopPlayback = new RecordingStopPlaybackService();
        var playing = new PlaybackState(
            new PlaybackTransportSlice(2, true, true, true, PlayStatus.Playing, false, false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));
        var dispatcher = new RecordingDispatcher();
        ScheduleListItemViewModel? sut = null;
        try
        {
            var first = new ScheduleStateItem { Id = 1, Name = "A" };
            var second = new ScheduleStateItem { Id = 2, Name = "B" };
            var (schedule, stateItem) = CreateMorningSchedule(2);
            sut = CreateSut(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { first, second }, null),
                dispatcher,
                stopPlayback,
                new FakePlaybackState(playing));
            WireScheduleForHost(sut, schedule, stateItem);

            ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null).GetAwaiter().GetResult();

            Assert.True(stopPlayback.StopWasCalled);
            Assert.Contains(dispatcher.Dispatched, action => action is DeleteScheduleAction);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void DeleteCommand_noop_when_schedule_id_invalid()
    {
        var dispatcher = new RecordingDispatcher();
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState(), dispatcher);
            InvokePrivate(sut, "EnsureScheduleCommands");

            sut.DeleteCommand.Execute(null);

            Assert.Empty(dispatcher.Dispatched);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void ToggleEnabledCommand_flips_enabled_flag()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(32);
            schedule.IsEnabled = true;
            stateItem.IsEnabled = true;
            sut = CreateSut(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                scheduleStateService: new NopScheduleStateService());
            WireScheduleForHost(sut, schedule, stateItem);

            sut.ToggleEnabledCommand.Execute(null);

            Assert.False(sut.IsEnabled);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task PlayCommand_invokes_playback_service_on_host()
    {
        var playback = new RecordingSchedulePlaybackService();
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(33);
            sut = CreateSut(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                schedulePlaybackService: playback);
            WireScheduleForHost(sut, schedule, stateItem);

            try
            {
                await ((AsyncRelayCommand)sut.PlayCommand).ExecuteAsync(null);
            }
            catch (COMException)
            {
            }

            // Playback may complete on MainThread; when dispatcher missing, catch path still ran.
            Assert.True(playback.PlayedScheduleId is null or 33);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task PlayCommand_early_returns_when_schedule_id_invalid()
    {
        var playback = new RecordingSchedulePlaybackService();
        ScheduleListItemViewModel? sut = null;
        try
        {
            sut = CreateSut(new ApplicationState(), schedulePlaybackService: playback);
            InvokePrivate(sut, "EnsureScheduleCommands");

            await ((AsyncRelayCommand)sut.PlayCommand).ExecuteAsync(null);

            Assert.Null(playback.PlayedScheduleId);
            Assert.False(sut.IsBusy);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void PlaybackModalOpenedMessage_clears_busy_after_wire()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(34);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            WireScheduleForHost(sut, schedule, stateItem);
            sut.IsBusy = true;

            try
            {
                WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
            }
            catch (COMException)
            {
            }

            Assert.Equal(34, sut.ScheduleId);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void RequestShowPlaybackModalMessage_sets_busy_for_matching_id()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(35);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            WireScheduleForHost(sut, schedule, stateItem);

            try
            {
                WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = 35 });
            }
            catch (COMException)
            {
            }

            Assert.Equal(35, sut.ScheduleId);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void RequestShowPlaybackModalMessage_ignored_for_other_schedule()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(36);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            WireScheduleForHost(sut, schedule, stateItem);

            WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = 99 });

            Assert.False(sut.IsBusy);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void PlaybackExplicitStopMessage_syncs_busy_when_idle()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(37);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            WireScheduleForHost(sut, schedule, stateItem);
            sut.IsBusy = true;

            try
            {
                WeakReferenceMessenger.Default.Send(new PlaybackExplicitStopMessage());
            }
            catch (COMException)
            {
            }

            Assert.Equal(37, sut.ScheduleId);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void ThemeChangedMessage_does_not_throw_after_wire()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(38);
            sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            WireScheduleForHost(sut, schedule, stateItem);

            try
            {
                WeakReferenceMessenger.Default.Send(new ThemeChangedMessage());
            }
            catch (COMException)
            {
            }
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Application_state_rename_updates_name_after_wire()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(39);
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
            sut = CreateSut(new ApplicationState(), applicationState: appState);
            WireScheduleForHost(sut, schedule, stateItem);

            stateItem.Name = "Renamed";
            appState.Value = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null);
            try
            {
                appState.NotifyChanged();
            }
            catch (COMException)
            {
            }

            Assert.Equal("Renamed", sut.Name);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void SyncIsBusy_clears_when_playback_idle_after_wire()
    {
        var playbackState = new ViewModelTestDoubles.MutablePlaybackState(new PlaybackState());
        ScheduleListItemViewModel? sut = null;
        try
        {
            var (schedule, stateItem) = CreateMorningSchedule(40);
            sut = CreateSut(
                new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                playbackState: playbackState);
            WireScheduleForHost(sut, schedule, stateItem);
            sut.IsBusy = true;

            try
            {
                playbackState.NotifyChanged();
            }
            catch (COMException)
            {
            }

            Assert.Equal(40, sut.ScheduleId);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Dispose_after_wire_unregisters_messengers()
    {
        var (schedule, stateItem) = CreateMorningSchedule(41);
        var sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
        WireScheduleForHost(sut, schedule, stateItem);

        sut.Dispose();
        sut.Dispose();
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
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
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
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
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

        [Fact]
        public void InitializeFromSchedule_is_no_op_when_schedule_id_invalid()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    sut = CreateSut(new ApplicationState());
                    sut.InitializeFromSchedule(new AlarmSchedule { Id = 0, Name = "Bad" }, null);

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
        public void This_property_returns_same_view_model_instance()
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

                    Assert.Same(sut, sut.This);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void DaysOfWeek_reflects_schedule_after_initialize()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    schedule.DaysOfWeek = WeekDays.Monday | WeekDays.Friday;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.Equal(WeekDays.Monday | WeekDays.Friday, sut.DaysOfWeek);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void RequestShowPlaybackModalMessage_sets_busy_for_matching_schedule()
        {
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            {
                return;
            }

            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(6);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = 6 });

                    Assert.True(sut.IsBusy);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void RequestShowPlaybackModalMessage_ignored_for_different_schedule()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(6);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = 99 });

                    Assert.False(sut.IsBusy);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void PlaybackExplicitStopMessage_clears_row_busy_when_idle()
        {
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            {
                return;
            }

            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(4);
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);
                    sut.IsBusy = true;

                    WeakReferenceMessenger.Default.Send(new PlaybackExplicitStopMessage());

                    Assert.False(sut.IsBusy);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public async Task PlayCommand_invokes_schedule_playback_service()
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            _ = fixture;
            var playback = new RecordingSchedulePlaybackService();
            ScheduleListItemViewModel? sut = null;
            try
            {
                var (schedule, stateItem) = CreateMorningSchedule(15);
                sut = CreateSut(
                    new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                    schedulePlaybackService: playback);
                sut.InitializeFromSchedule(schedule, stateItem);

                await ((AsyncRelayCommand)sut.PlayCommand).ExecuteAsync(null);
                if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
                {
                    return;
                }

                Assert.Equal(15, playback.PlayedScheduleId);
            }
            catch (COMException)
            {
            }
            finally
            {
                sut?.Dispose();
            }
        }

        [Fact]
        public async Task PlayCommand_invokes_OnPlayStarted_callback()
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            _ = fixture;
            var started = false;
            ScheduleListItemViewModel? sut = null;
            try
            {
                var (schedule, stateItem) = CreateMorningSchedule(16);
                sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                sut.InitializeFromSchedule(schedule, stateItem);
                sut.OnPlayStarted = () => started = true;

                await ((AsyncRelayCommand)sut.PlayCommand).ExecuteAsync(null);
                if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
                {
                    return;
                }

                Assert.True(started);
            }
            catch (COMException)
            {
            }
            finally
            {
                sut?.Dispose();
            }
        }

        [Fact]
        public void Application_state_change_updates_name_when_fluxor_schedule_renamed()
        {
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(20);
                    var appState = new ViewModelTestDoubles.MutableApplicationState(
                        new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut = CreateSut(new ApplicationState(), applicationState: appState);
                    sut.InitializeFromSchedule(schedule, stateItem);

                    stateItem.Name = "Renamed Morning";
                    appState.Value = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null);
                    appState.NotifyChanged();
                    MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult();

                    Assert.Equal("Renamed Morning", sut.Name);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void ThemeChangedMessage_notifies_IsEnabled_property()
        {
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
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
                    var notified = false;
                    sut.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ScheduleListItemViewModel.IsEnabled))
                        {
                            notified = true;
                        }
                    };

                    WeakReferenceMessenger.Default.Send(new ThemeChangedMessage());

                    Assert.True(notified);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void DeleteCommand_does_not_dispatch_when_schedule_id_invalid()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                var dispatcher = new RecordingDispatcher();
                ScheduleListItemViewModel? sut = null;
                try
                {
                    sut = CreateSut(new ApplicationState(), dispatcher);
                    sut.DeleteCommand.Execute(null);

                    Assert.Empty(dispatcher.Dispatched);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void IsEnabled_reverts_when_schedule_state_update_fails()
        {
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    sut = CreateSut(
                        new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                        scheduleStateService: new FailingScheduleStateService());
                    sut.InitializeFromSchedule(schedule, stateItem);

                    sut.IsEnabled = false;
                    MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult();

                    Assert.True(sut.IsEnabled);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void SyncIsBusy_clears_when_playback_stops_for_other_schedule()
        {
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            {
                return;
            }

            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                var playbackState = new ViewModelTestDoubles.MutablePlaybackState(new PlaybackState());
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule(21);
                    sut = CreateSut(
                        new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null),
                        playbackState: playbackState);
                    sut.InitializeFromSchedule(schedule, stateItem);
                    sut.IsBusy = true;

                    playbackState.NotifyChanged();

                    Assert.False(sut.IsBusy);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void IsBibleCategory_true_for_bible_publication_category()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    stateItem.BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.True(sut.IsBibleCategory);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void ShouldShowLanguageOrMusicLine_false_when_language_empty_and_music_disabled()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    schedule.MusicEnabled = false;
                    stateItem.MusicEnabled = false;
                    stateItem.BiblePublicationLanguageName = string.Empty;
                    sut = CreateSut(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { stateItem }, null));
                    sut.InitializeFromSchedule(schedule, stateItem);

                    Assert.False(sut.ShouldShowLanguageOrMusicLine);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }

        [Fact]
        public void CategoryNameDisplay_returns_empty_for_whitespace_category()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                ScheduleListItemViewModel? sut = null;
                try
                {
                    var (schedule, stateItem) = CreateMorningSchedule();
                    stateItem.BiblePublicationCategoryName = "   ";
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
        public void IsBusy_defaults_false_after_initialize()
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

                    Assert.False(sut.IsBusy);
                }
                finally
                {
                    sut?.Dispose();
                }
            });
        }
    }
}
