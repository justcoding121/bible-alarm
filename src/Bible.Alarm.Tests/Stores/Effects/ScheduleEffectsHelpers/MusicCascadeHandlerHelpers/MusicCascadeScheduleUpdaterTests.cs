#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicCascadeScheduleUpdaterTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action) => Dispatched.Add(action);

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private static MusicCascadeScheduleMutation Mutation(string trackTitle, int? pubModal = 1, int? secModal = 2) =>
        new(
            PublicationCode: "sjj",
            PublicationName: "Sing",
            SectionCode: null,
            SectionName: string.Empty,
            TrackCode: "5",
            TrackTitle: trackTitle,
            PublicationModalItemCount: pubModal,
            SectionModalItemCount: secModal);

    [Fact]
    public void UpdateSchedule_skips_dispatch_when_values_unchanged()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            Id = 9,
            MusicPublicationCode = "sjj",
            MusicSectionCode = null,
            MusicTrackCode = "5",
            MusicTrackName = "Same",
            MusicPublicationModalItemCount = 1,
            MusicSectionModalItemCount = 2,
        };

        MusicCascadeScheduleUpdater.UpdateSchedule(TestLogging.CreateLogger(), current, Mutation("Same"), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void UpdateSchedule_dispatches_when_track_title_changes()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            Id = 9,
            MusicPublicationCode = "sjj",
            MusicSectionCode = null,
            MusicTrackCode = "5",
            MusicTrackName = "Old",
            MusicPublicationModalItemCount = 1,
            MusicSectionModalItemCount = 2,
        };

        MusicCascadeScheduleUpdater.UpdateSchedule(TestLogging.CreateLogger(), current, Mutation("New"), dispatcher);

        var action = Assert.Single(dispatcher.Dispatched);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
        Assert.Equal("New", update.Schedule.MusicTrackName);
        Assert.True(update.MusicUpdated);
        Assert.False(update.BiblePublicationUpdated);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void UpdateSchedule_dispatches_when_modal_count_changes()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            Id = 9,
            MusicPublicationCode = "sjj",
            MusicSectionCode = null,
            MusicTrackCode = "5",
            MusicTrackName = "T",
            MusicPublicationModalItemCount = 1,
            MusicSectionModalItemCount = 2,
        };

        MusicCascadeScheduleUpdater.UpdateSchedule(TestLogging.CreateLogger(), current, Mutation("T", pubModal: 9, secModal: 2), dispatcher);

        var action = Assert.Single(dispatcher.Dispatched);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
        Assert.Equal(9, update.Schedule.MusicPublicationModalItemCount);
    }
}
