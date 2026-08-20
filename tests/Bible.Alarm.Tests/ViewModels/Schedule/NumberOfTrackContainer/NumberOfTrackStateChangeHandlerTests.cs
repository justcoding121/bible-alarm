#nullable enable

using System.Reflection;
using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTrackStateChangeHandlerTests
{
    private sealed class MutableApplicationState : IState<ApplicationState>
    {
        public MutableApplicationState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static NumberOfTrackContainerViewModel CreateSut(
        ScheduleStateItem schedule,
        RecordingDispatcher dispatcher)
    {
        var fluxorState = new MutableApplicationState(new ApplicationState(
            new ObservableHashSet<ScheduleStateItem>(),
            schedule));
        return new NumberOfTrackContainerViewModel(
            TestLogging.CreateLogger(),
            new UnusedNavigationServiceStub(),
            new EmptyServiceProvider(),
            fluxorState,
            dispatcher,
            CreateMapper());
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static T? GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T?)field!.GetValue(target);
    }

    [Fact]
    public void Syncs_notification_toggle_and_schedule_fields()
    {
        var initial = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = false,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 1,
            BiblePublicationCategoryName = "Bible",
        };
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = true,
            NumberOfTracksToPlay = 2,
            BiblePublicationCategoryName = "Bible",
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(initial, dispatcher);
        SetPrivateField(sut, "notificationEnabled", false);
        SetPrivateField(sut, "lastCategoryName", "Bible");
        dispatcher.Dispatched.Clear();

        sut.ApplyScheduleStatePropertyChangesCore(schedule);

        Assert.True(sut.NotificationEnabled);
        Assert.True(sut.AlwaysPlayFromStart);
        Assert.False(sut.PlayIndefinitely);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void Category_change_repairs_list_and_dispatches_when_track_cap_positive()
    {
        var initial = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
            BiblePublicationCategoryName = "Bible",
        };
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
            BiblePublicationCategoryName = "Dramas",
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(initial, dispatcher);
        SetPrivateField(sut, "lastCategoryName", "Bible");
        dispatcher.Dispatched.Clear();

        sut.ApplyScheduleStatePropertyChangesCore(schedule);

        Assert.Equal("Dramas", GetPrivateField<string>(sut, "lastCategoryName"));
        Assert.Contains(dispatcher.Dispatched, a =>
            a is UpdateScheduleFromViewModelAction update && update.Schedule.NumberOfTracksToPlay == 1);
    }

    [Fact]
    public void Category_change_skips_dispatch_when_playing_indefinitely()
    {
        var initial = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 0,
            BiblePublicationCategoryName = "Bible",
        };
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 0,
            BiblePublicationCategoryName = "Music",
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(initial, dispatcher);
        SetPrivateField(sut, "lastCategoryName", "Bible");
        dispatcher.Dispatched.Clear();

        sut.ApplyScheduleStatePropertyChangesCore(schedule);

        Assert.True(sut.PlayIndefinitely);
        Assert.DoesNotContain(dispatcher.Dispatched, a =>
            a is UpdateScheduleFromViewModelAction update && update.Schedule.NumberOfTracksToPlay == 1);
    }

#if ANDROID || IOS
    [Fact]
    public void Skips_notification_resync_while_waiting_for_permission_response()
    {
        var initial = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = false,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 1,
            BiblePublicationCategoryName = "Bible",
        };
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 1,
            BiblePublicationCategoryName = "Bible",
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(initial, dispatcher);
        SetPrivateField(sut, "notificationEnabled", false);
        SetPrivateField(sut, "lastCategoryName", "Bible");
        SetPrivateField(sut, "isWaitingForPermissionResponse", true);
        dispatcher.Dispatched.Clear();

        sut.ApplyScheduleStatePropertyChangesCore(schedule);

        Assert.False(sut.NotificationEnabled);
        Assert.False(sut.AlwaysPlayFromStart);
        Assert.False(sut.PlayIndefinitely);
        Assert.Empty(dispatcher.Dispatched);
    }
#endif

    [Fact]
    public void Category_change_ignored_when_name_differs_only_by_case()
    {
        var initial = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
            BiblePublicationCategoryName = "dramas",
        };
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
            BiblePublicationCategoryName = "DRAMAS",
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(initial, dispatcher);
        SetPrivateField(sut, "lastCategoryName", "dramas");
        dispatcher.Dispatched.Clear();

        sut.ApplyScheduleStatePropertyChangesCore(schedule);

        Assert.Equal("DRAMAS", GetPrivateField<string>(sut, "lastCategoryName"));
        Assert.DoesNotContain(dispatcher.Dispatched, a =>
            a is UpdateScheduleFromViewModelAction update && update.Schedule.NumberOfTracksToPlay == 1);
    }
}
