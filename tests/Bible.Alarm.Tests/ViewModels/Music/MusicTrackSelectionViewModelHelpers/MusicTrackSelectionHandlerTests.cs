#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Maui.Controls;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackSelectionHandlerTests
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
    public async Task HandleTrackSelection_no_op_when_track_null()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState());
        var sut = new MusicTrackSelectionHandler(dispatcher, state, navigation);

        await sut.HandleTrackSelection(null!, null);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCount);
    }

    [Fact]
    public async Task HandleTrackSelection_no_op_when_publication_code_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { MusicPublicationCode = null }
        });
        var track = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "1", Title = "T" });
        var sut = new MusicTrackSelectionHandler(dispatcher, state, navigation);

        await sut.HandleTrackSelection(track, new AlarmMusic());

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleTrackSelection_uses_section_fields_for_iam_publication()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem
            {
                MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                MusicLanguageCode = null,
                MusicSectionCode = "iam-1",
                MusicSectionName = "Disc 1",
                MusicPublicationName = "Kingdom Melodies"
            }
        });
        var track = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "9", Title = "Song" });
        var sut = new MusicTrackSelectionHandler(dispatcher, state, navigation);

        await sut.HandleTrackSelection(track, new AlarmMusic());

        var item = Assert.IsType<TrackSelectedAction>(Assert.Single(dispatcher.Dispatched)).CurrentMusic;
        Assert.Equal("iam-1", item.SectionCode);
        Assert.Equal("Disc 1", item.SectionName);
    }

    [Fact]
    public async Task HandleTrackSelection_clears_section_for_flat_vocal_publication()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigationService();
        var state = new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem
            {
                MusicPublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                MusicLanguageCode = "E",
                MusicSectionCode = "should-not-map",
                MusicSectionName = "ignored",
                MusicPublicationName = "OSG"
            }
        });
        var track = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "3", Title = "Vocal" });
        var sut = new MusicTrackSelectionHandler(dispatcher, state, navigation);

        await sut.HandleTrackSelection(track, new AlarmMusic());

        var item = Assert.IsType<TrackSelectedAction>(Assert.Single(dispatcher.Dispatched)).CurrentMusic;
        Assert.Null(item.SectionCode);
        Assert.Null(item.SectionName);
    }
}
