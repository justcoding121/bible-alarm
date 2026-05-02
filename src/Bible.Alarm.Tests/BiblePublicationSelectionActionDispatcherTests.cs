#nullable enable

using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionActionDispatcherTests
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

    private static BiblePublicationStateItem SampleItem() =>
        new()
        {
            Id = 1,
            LanguageCode = "E",
            PublicationCode = "nwtsty",
            TrackCode = "1",
            AlarmScheduleId = 99,
        };

    [Fact]
    public void DispatchBiblePublicationSelectionActions_dispatches_track_selected_only()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new BiblePublicationSelectionActionDispatcher(dispatcher);
        var item = SampleItem();

        sut.DispatchBiblePublicationSelectionActions(item);

        var trackSelected = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<TrackSelectedAction>(trackSelected);
        Assert.Same(item, action.CurrentBiblePublicationSchedule);
    }

    [Fact]
    public void DispatchLanguageSelectionActions_dispatches_track_selected_only()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new BiblePublicationSelectionActionDispatcher(dispatcher);
        var item = SampleItem();

        sut.DispatchLanguageSelectionActions(item);

        var trackSelected = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<TrackSelectedAction>(trackSelected);
        Assert.Same(item, action.CurrentBiblePublicationSchedule);
    }
}
