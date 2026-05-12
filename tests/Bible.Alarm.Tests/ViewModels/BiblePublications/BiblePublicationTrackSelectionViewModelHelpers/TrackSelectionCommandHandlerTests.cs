#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Maui.Controls;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionCommandHandlerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

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

    private sealed class CountingNavigationService : INavigationService
    {
        public int PopModalCount { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;
        public Task NavigateToScheduleAsync() => Task.CompletedTask;
        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;
        public Task OpenSongPublicationSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenLanguageModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;
        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task PopModalAsync()
        {
            PopModalCount++;
            return Task.CompletedTask;
        }

        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Views.Home? GetCurrentHomePage() => null;
        public Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    [Fact]
    public async Task HandleSetTrackAsync_no_op_when_track_null()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState());
        var sut = new TrackSelectionCommandHandler(TestLogging.CreateLogger(), state, dispatcher, navigation);

        await sut.HandleSetTrackAsync(null!);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCount);
    }

    [Fact]
    public async Task HandleSetTrackAsync_no_op_when_schedule_missing_language()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem
            {
                BiblePublicationCode = "nwt",
                BiblePublicationLanguageCode = null
            }
        });
        var trackVm = new BiblePublicationTrackListViewItemModel(new BiblePublicationTrack { TrackCode = "1", Title = "A" });
        var sut = new TrackSelectionCommandHandler(TestLogging.CreateLogger(), state, dispatcher, navigation);

        await sut.HandleSetTrackAsync(trackVm);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCount);
    }

    [Fact]
    public async Task HandleSetTrackAsync_dispatches_and_pops_when_schedule_valid()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem
            {
                BiblePublicationLanguageCode = "E",
                BiblePublicationCode = "nwt",
                BiblePublicationSectionCode = "1",
                BiblePublicationLanguageName = "English",
                BiblePublicationLanguageDirection = "ltr",
                BiblePublicationName = "NWT",
                BiblePublicationSectionName = "Genesis"
            }
        });
        var trackVm = new BiblePublicationTrackListViewItemModel(new BiblePublicationTrack { TrackCode = "42", Title = "Verse" });
        var sut = new TrackSelectionCommandHandler(TestLogging.CreateLogger(), state, dispatcher, navigation);

        await sut.HandleSetTrackAsync(trackVm);

        var action = Assert.Single(dispatcher.Dispatched);
        var selected = Assert.IsType<TrackSelectedAction>(action).CurrentBiblePublicationSchedule;
        Assert.Equal("42", selected.TrackCode);
        Assert.Equal("1", selected.SectionCode);
        Assert.Equal(1, navigation.PopModalCount);
    }
}
