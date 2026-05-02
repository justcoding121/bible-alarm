#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationCascadeScheduleUpdaterTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action) => Dispatched.Add(action);

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private static BiblePublicationCascadeScheduleMutation Mutation(
        string trackTitle,
        bool publicationWithoutLanguage = false) =>
        new(
            PublicationCode: "nwt",
            PublicationName: "NWT",
            SectionCode: "40",
            SectionName: "Matthew",
            TrackCode: "1",
            TrackTitle: trackTitle,
            PublicationModalItemCount: 1,
            SectionModalItemCount: 2,
            TrackModalItemCount: 3,
            PublicationWithoutLanguage: publicationWithoutLanguage);

    [Fact]
    public void UpdateSchedule_skips_dispatch_when_values_unchanged()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            Id = 5,
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "1",
            BiblePublicationTrackTitle = "Same",
            BiblePublicationModalItemCount = 1,
            BiblePublicationSectionModalItemCount = 2,
            BiblePublicationTrackModalItemCount = 3,
        };

        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            TestLogging.CreateLogger(),
            current,
            Mutation("Same"),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void UpdateSchedule_dispatches_when_track_title_changes()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            Id = 5,
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "1",
            BiblePublicationTrackTitle = "Old",
            BiblePublicationModalItemCount = 1,
            BiblePublicationSectionModalItemCount = 2,
            BiblePublicationTrackModalItemCount = 3,
        };

        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            TestLogging.CreateLogger(),
            current,
            Mutation("New"),
            dispatcher);

        var action = Assert.Single(dispatcher.Dispatched);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
        Assert.Equal("New", update.Schedule.BiblePublicationTrackTitle);
        Assert.False(update.MusicUpdated);
        Assert.False(update.BiblePublicationUpdated);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void UpdateSchedule_sets_default_language_when_publication_without_language_and_missing_language()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            Id = 8,
            BiblePublicationCode = "iam",
            BiblePublicationLanguageCode = null,
            BiblePublicationLanguageName = null,
            BiblePublicationLanguageDirection = null,
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = "a",
            BiblePublicationTrackTitle = "OldTitle",
            BiblePublicationModalItemCount = 0,
            BiblePublicationSectionModalItemCount = 0,
            BiblePublicationTrackModalItemCount = 0,
        };

        var mutation = new BiblePublicationCascadeScheduleMutation(
            PublicationCode: "iam",
            PublicationName: "IAM",
            SectionCode: null,
            SectionName: "",
            TrackCode: "b",
            TrackTitle: "NewTitle",
            PublicationModalItemCount: 0,
            SectionModalItemCount: 0,
            TrackModalItemCount: 0,
            PublicationWithoutLanguage: true);

        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            TestLogging.CreateLogger(),
            current,
            mutation,
            dispatcher);

        var update = Assert.Single(dispatcher.Dispatched);
        var vmAction = Assert.IsType<UpdateScheduleFromViewModelAction>(update);
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, vmAction.Schedule.BiblePublicationLanguageCode);
        Assert.Equal("b", vmAction.Schedule.BiblePublicationTrackCode);
    }
}
