#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemStateChangeApplierTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem StateItem(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 7,
            Minute = 15,
            DaysOfWeek = WeekDays.Monday,
            BiblePublicationCategoryName = "Bible",
            BiblePublicationLanguageName = "English",
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackTitle = "Chapter 1",
            BiblePublicationCode = "nwt",
        };

    private static (ScheduleListItemStateChangeApplier applier, ScheduleListItemStateHandler handler, FakeApplicationState appState)
        CreateApplier(int scheduleId, ScheduleStateItem stateItem)
    {
        var schedules = new ObservableHashSet<ScheduleStateItem> { stateItem };
        var appState = new FakeApplicationState(new ApplicationState(schedules, null));
        var mapper = CreateMapper();
        var handler = new ScheduleListItemStateHandler(TestLogging.CreateLogger(), mapper, appState);
        handler.LastKnownSectionName = stateItem.BiblePublicationSectionName;
        handler.LastKnownTrackTitle = stateItem.BiblePublicationTrackTitle;
        handler.LastKnownBiblePublicationLanguageName = stateItem.BiblePublicationLanguageName;
        handler.LastKnownBiblePublicationCode = stateItem.BiblePublicationCode;
        var enabled = stateItem.IsEnabled;
        var applier = new ScheduleListItemStateChangeApplier(
            TestLogging.CreateLogger(),
            appState,
            handler,
            value => enabled = value);
        return (applier, handler, appState);
    }

    private static void RunOnMainThreadOrSkip(Action body)
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        try
        {
            body();
            MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult();
        }
        catch (COMException)
        {
        }
    }

    [Fact]
    public void UpdateScheduleFromState_skips_subtitle_when_bible_properties_unchanged()
    {
        var id = 3;
        var item = StateItem(id, "Morning");
        var (_, handler, appState) = CreateApplier(id, item);
        var mapper = CreateMapper();
        var current = mapper.Map<AlarmSchedule>(item);
        handler.LastKnownSchedule = current;
        var enabled = true;
        var applier = new ScheduleListItemStateChangeApplier(
            TestLogging.CreateLogger(),
            appState,
            handler,
            value => enabled = value);
        var refreshCalled = false;
        AlarmSchedule? assigned = null;

        var changeInfo = new ScheduleListItemStateHandler.ScheduleChangeInfo
        {
            UpdatedSchedule = current,
            AnyBibleSchedulePropertyChanged = false,
        };

        applier.UpdateScheduleFromState(
            changeInfo,
            s => assigned = s,
            _ => refreshCalled = true,
            _ => { },
            () => id);

        Assert.Same(current, assigned);
        Assert.False(refreshCalled);
    }

    [Fact]
    public void UpdateScheduleFromState_applies_enabled_from_updated_schedule()
    {
        var id = 4;
        var item = StateItem(id, "Evening");
        item.IsEnabled = false;
        var (_, handler, appState) = CreateApplier(id, item);
        var mapper = CreateMapper();
        var updated = mapper.Map<AlarmSchedule>(item);
        updated.IsEnabled = false;
        var enabled = true;
        var applier = new ScheduleListItemStateChangeApplier(
            TestLogging.CreateLogger(),
            appState,
            handler,
            value => enabled = value);

        applier.UpdateScheduleFromState(
            new ScheduleListItemStateHandler.ScheduleChangeInfo
            {
                UpdatedSchedule = updated,
                AnyBibleSchedulePropertyChanged = false,
            },
            _ => { },
            _ => { },
            _ => { },
            () => id);

        Assert.False(enabled);
    }

    [Fact]
    public void UpdateScheduleFromState_refreshes_subtitle_when_bible_display_changed()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 5;
            var item = StateItem(id, "Noon");
            var (_, handler, appState) = CreateApplier(id, item);
            var mapper = CreateMapper();
            var updated = mapper.Map<AlarmSchedule>(item);
            item.BiblePublicationSectionName = "Exodus";
            var refreshCalled = false;

            var applier = new ScheduleListItemStateChangeApplier(
                TestLogging.CreateLogger(),
                appState,
                handler,
                _ => { });

            applier.UpdateScheduleFromState(
                new ScheduleListItemStateHandler.ScheduleChangeInfo
                {
                    UpdatedSchedule = updated,
                    AnyBibleSchedulePropertyChanged = true,
                    NewSectionName = "Exodus",
                    NewTrackTitle = item.BiblePublicationTrackTitle,
                    BiblePublicationCodeChanged = false,
                },
                _ => { },
                _ => refreshCalled = true,
                _ => { },
                () => id);

            Assert.True(refreshCalled);
        });
    }

    [Fact]
    public void NotifyPropertyChanges_notifies_DaysOfWeek_when_flag_set()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 6;
            var item = StateItem(id, "Days");
            var (applier, handler, appState) = CreateApplier(id, item);
            var mapper = CreateMapper();
            var schedule = mapper.Map<AlarmSchedule>(item);
            handler.LastKnownSchedule = schedule;
            var notified = new List<string>();

            applier.NotifyPropertyChanges(
                new ScheduleListItemStateHandler.ScheduleChangeInfo { DaysOfWeekChanged = true },
                name => notified.Add(name),
                () => id,
                () => schedule,
                () => { });

            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.DaysOfWeek), notified);
        });
    }

    [Fact]
    public void NotifyPropertyChanges_notifies_Name_when_flag_set()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 7;
            var item = StateItem(id, "Renamed");
            var (applier, _, _) = CreateApplier(id, item);
            var notified = new List<string>();

            applier.NotifyPropertyChanges(
                new ScheduleListItemStateHandler.ScheduleChangeInfo { NameChanged = true },
                name => notified.Add(name),
                () => id,
                () => null,
                () => { });

            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Name), notified);
        });
    }

    [Fact]
    public void NotifyPropertyChanges_notifies_time_fields_when_TimeChanged()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 8;
            var item = StateItem(id, "Time");
            var (applier, _, _) = CreateApplier(id, item);
            var notified = new HashSet<string>(StringComparer.Ordinal);

            applier.NotifyPropertyChanges(
                new ScheduleListItemStateHandler.ScheduleChangeInfo { TimeChanged = true },
                name => notified.Add(name),
                () => id,
                () => null,
                () => { });

            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.TimeText), notified);
            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Hour), notified);
            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Minute), notified);
        });
    }

    [Fact]
    public void NotifyPropertyChanges_notifies_MusicEnabled_when_flag_set()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 9;
            var item = StateItem(id, "Music");
            var (applier, _, _) = CreateApplier(id, item);
            var notified = new List<string>();

            applier.NotifyPropertyChanges(
                new ScheduleListItemStateHandler.ScheduleChangeInfo { MusicEnabledChanged = true },
                name => notified.Add(name),
                () => id,
                () => null,
                () => { });

            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.MusicEnabled), notified);
        });
    }

    [Fact]
    public void NotifyPropertyChanges_sends_hide_progress_when_IsEnabled_changed()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 10;
            var item = StateItem(id, "Toggle");
            var (applier, _, _) = CreateApplier(id, item);
            var hideCount = 0;
            WeakReferenceMessenger.Default.Register<HideProgressBarMessage>(this, (_, _) => hideCount++);

            try
            {
                applier.NotifyPropertyChanges(
                    new ScheduleListItemStateHandler.ScheduleChangeInfo { IsEnabledChanged = true },
                    _ => { },
                    () => id,
                    () => null,
                    () => { });

                Assert.Equal(1, hideCount);
            }
            finally
            {
                WeakReferenceMessenger.Default.Unregister<HideProgressBarMessage>(this);
            }
        });
    }

    [Fact]
    public void NotifyPropertyChanges_notifies_subtitle_properties_when_bible_changed()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 11;
            var item = StateItem(id, "Bible");
            var (applier, _, _) = CreateApplier(id, item);
            var notified = new HashSet<string>(StringComparer.Ordinal);

            applier.NotifyPropertyChanges(
                new ScheduleListItemStateHandler.ScheduleChangeInfo { AnyBibleSchedulePropertyChanged = true },
                name => notified.Add(name),
                () => id,
                () => null,
                () => { });

            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.SubTitle), notified);
            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Language), notified);
        });
    }

    [Fact]
    public void NotifyPropertyChanges_invokes_raisePropertiesChangedEvent()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 12;
            var item = StateItem(id, "Raise");
            var (applier, _, _) = CreateApplier(id, item);
            var raised = false;

            applier.NotifyPropertyChanges(
                new ScheduleListItemStateHandler.ScheduleChangeInfo(),
                _ => { },
                () => id,
                () => null,
                () => raised = true);

            Assert.True(raised);
        });
    }

    [Fact]
    public void UpdateScheduleFromState_notifies_BiblePublicationName_when_code_changed()
    {
        RunOnMainThreadOrSkip(() =>
        {
            var id = 13;
            var item = StateItem(id, "Pub");
            item.BiblePublicationCode = "nwtsty";
            var (_, handler, appState) = CreateApplier(id, item);
            var mapper = CreateMapper();
            var updated = mapper.Map<AlarmSchedule>(item);
            var notified = new List<string>();

            var applier = new ScheduleListItemStateChangeApplier(
                TestLogging.CreateLogger(),
                appState,
                handler,
                _ => { });

            applier.UpdateScheduleFromState(
                new ScheduleListItemStateHandler.ScheduleChangeInfo
                {
                    UpdatedSchedule = updated,
                    AnyBibleSchedulePropertyChanged = true,
                    BiblePublicationCodeChanged = true,
                    NewSectionName = item.BiblePublicationSectionName,
                    NewTrackTitle = item.BiblePublicationTrackTitle,
                },
                _ => { },
                _ => { },
                name => notified.Add(name),
                () => id);

            Assert.Contains(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.BiblePublicationName), notified);
        });
    }
}
