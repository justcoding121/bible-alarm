#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;
using Bible.Alarm.Views;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionSelectionTrackTapHandlerTests
{
    private sealed class FakeState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

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

    private sealed class CountingNavigation : INavigationService
    {
        public int PopModalCalls { get; private set; }

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
            PopModalCalls++;
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

        public Home? GetCurrentHomePage() => null;
        public Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private static ScheduleStateItem ScheduleWithBible(string publicationCode, string? languageCode) =>
        new()
        {
            Id = 1,
            Name = "S",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            BiblePublicationCode = publicationCode,
            BiblePublicationLanguageCode = languageCode,
        };

    private static BiblePublicationSectionSelectionTrackTapHandler CreateSut(
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigation) =>
        new(
            TestLogging.CreateLogger(),
            state,
            dispatcher,
            navigation,
            new TrackSelectionResolver(TestLogging.CreateLogger(), new IdleCatalogMediaService()));

    [Fact]
    public async Task HandleSectionTapAsync_no_op_when_no_current_schedule()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var sut = CreateSut(new FakeState(new ApplicationState()), dispatcher, navigation);
        var section = new BiblePublicationSection { SectionCode = "1", Name = "A" };
        var item = new BiblePublicationSectionListViewItemModel(section);

        await sut.HandleSectionTapAsync(item);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task HandleSectionTapAsync_no_op_when_publication_code_missing()
    {
        var schedule = ScheduleWithBible(publicationCode: "", languageCode: "E");
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var sut = CreateSut(new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule)), dispatcher, navigation);
        var item = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1", Name = "A" });

        await sut.HandleSectionTapAsync(item);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task HandleSectionTapAsync_no_op_when_section_code_blank()
    {
        var schedule = ScheduleWithBible("nwt", languageCode: "E");
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var sut = CreateSut(new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule)), dispatcher, navigation);
        var item = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "  ", Name = "X" });

        await sut.HandleSectionTapAsync(item);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task HandleSectionTapAsync_no_op_when_resolver_returns_no_tracks()
    {
        var schedule = ScheduleWithBible("nwt", languageCode: "");
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var sut = CreateSut(new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule)), dispatcher, navigation);
        var item = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "mat", Name = "Matthew" });

        await sut.HandleSectionTapAsync(item);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCalls);
    }
}
