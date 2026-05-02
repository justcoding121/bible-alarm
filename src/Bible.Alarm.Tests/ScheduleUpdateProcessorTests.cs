#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleUpdateProcessorTests
{
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

    [Fact]
    public void HandleServiceUnavailable_dispatches_failure_with_message()
    {
        var dispatcher = new RecordingDispatcher();
        var schedule = new ScheduleStateItem { Id = 5, Name = "Morning" };
        var action = new UpdateScheduleFromViewModelAction(schedule);

        ScheduleUpdateProcessor.HandleServiceUnavailable(action, dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFailureAction>());
        Assert.Same(schedule, fail.Schedule);
        Assert.Equal("Service unavailable", fail.Error);
    }

    [Fact]
    public void PreserveMusicPropertiesIfNeeded_copies_from_action_when_publication_mismatches()
    {
        var mapped = new ScheduleStateItem
        {
            MusicPublicationCode = "oldpub",
            MusicTrackCode = "1",
            MusicLanguageCode = "E",
            MusicRepeat = false,
            MusicId = 1,
            MusicSectionCode = "s1",
        };
        var actionSchedule = new ScheduleStateItem
        {
            MusicPublicationCode = "newpub",
            MusicTrackCode = "9",
            MusicLanguageCode = "X",
            MusicRepeat = true,
            MusicId = 2,
            MusicSectionCode = "s2",
        };

        ScheduleUpdateProcessor.PreserveMusicPropertiesIfNeeded(mapped, actionSchedule);

        Assert.Equal("newpub", mapped.MusicPublicationCode);
        Assert.Equal("9", mapped.MusicTrackCode);
        Assert.Equal("X", mapped.MusicLanguageCode);
        Assert.True(mapped.MusicRepeat);
        Assert.Equal(2, mapped.MusicId);
        Assert.Equal("s2", mapped.MusicSectionCode);
    }

    [Fact]
    public void PreserveMusicPropertiesIfNeeded_no_op_when_action_publication_empty()
    {
        var mapped = new ScheduleStateItem { MusicPublicationCode = "iam" };
        var actionSchedule = new ScheduleStateItem { MusicPublicationCode = "" };

        ScheduleUpdateProcessor.PreserveMusicPropertiesIfNeeded(mapped, actionSchedule);

        Assert.Equal("iam", mapped.MusicPublicationCode);
    }

    [Fact]
    public void LogScheduleUpdateResult_does_not_throw()
    {
        var saved = new AlarmSchedule { Id = 3, Name = "Test", Music = null };
        ScheduleUpdateProcessor.LogScheduleUpdateResult(saved);
    }

    [Fact]
    public void LogMappingResult_does_not_throw()
    {
        var item = new ScheduleStateItem { MusicPublicationCode = "osg", MusicLanguageCode = "E", MusicTrackCode = "1" };
        ScheduleUpdateProcessor.LogMappingResult(item);
    }

    [Fact]
    public void LogUpdateStart_does_not_throw()
    {
        var schedule = new ScheduleStateItem { Id = 8, Name = "Z" };
        var action = new UpdateScheduleFromViewModelAction(schedule, shouldSave: false);
        ScheduleUpdateProcessor.LogUpdateStart(action);
    }
}
