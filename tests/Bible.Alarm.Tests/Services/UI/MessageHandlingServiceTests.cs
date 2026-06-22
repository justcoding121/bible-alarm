#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class MessageHandlingServiceTests(MauiUiFixture fixture)
{
    private sealed class RecordingNavigationService : INavigationService
    {
        public int NavigateToHomeCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true)
        {
            NavigateToHomeCalls++;
            return Task.CompletedTask;
        }

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

        public Task PopModalAsync() => Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Views.Home? GetCurrentHomePage() => null;

        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;

        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class RecordingPlaybackModalService : IPlaybackModalService
    {
        public int ShowOnWindowCreationCalls { get; private set; }

        public bool IsMinimized => false;

        public bool IsModalOpenOrPending => false;

        public void Dispose()
        {
        }

        public Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync()
        {
            ShowOnWindowCreationCalls++;
            return Task.FromResult(false);
        }

        public Task ShowPlaybackModalIfNeededOnResumeAsync() => Task.CompletedTask;

        public void ShowMiniBarIfPlaybackActiveOnResume()
        {
        }

        public void SubscribeToPlaybackStateChanges()
        {
        }

        public void UnsubscribeToPlaybackStateChanges()
        {
        }

        public bool WasRecentlyMinimized() => false;
    }

    private sealed class RecordingToastService : IToastService
    {
        public string? LastMessage { get; private set; }

        public void Dispose()
        {
        }

        public Task ShowMessage(string message, int seconds = 3)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task ShowScheduledNotification(Bible.Alarm.Shared.Models.Schedule.AlarmSchedule schedule, int seconds = 3) =>
            Task.CompletedTask;

        public Task Clear() => Task.CompletedTask;
    }

    private static MessageHandlingService CreateSut(
        IServiceProvider serviceProvider,
        INavigationService navigation,
        IPlaybackModalService playbackModal)
    {
        return new MessageHandlingService(
            TestLogging.CreateLogger(),
            serviceProvider,
            navigation,
            playbackModal);
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        _ = fixture;
        var sut = CreateSut(null!, new UnusedNavigationServiceStub(), new RecordingPlaybackModalService());
        Assert.NotNull(sut);
    }

    [Fact]
    public async Task RegisterMessageHandlers_delivers_initialized_message_to_handler()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var navigation = new RecordingNavigationService();
        var playbackModal = new RecordingPlaybackModalService();
        var sut = CreateSut(null!, navigation, playbackModal);
        sut.RegisterMessageHandlers();

        WeakReferenceMessenger.Default.Send(new InitializedMessage());
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            sut.Dispose();
            return;
        }
        await Task.Delay(200);

        Assert.Equal(1, navigation.NavigateToHomeCalls);
        Assert.Equal(1, playbackModal.ShowOnWindowCreationCalls);

        sut.Dispose();
    }

    [Fact]
    public async Task Receive_ShowToastMessage_invokes_toast_service()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var toast = new RecordingToastService();
        var services = new ServiceCollection();
        services.AddSingleton<IToastService>(toast);
        var serviceProvider = services.BuildServiceProvider();
        var sut = CreateSut(serviceProvider, new UnusedNavigationServiceStub(), new RecordingPlaybackModalService());

        sut.Receive(new ShowToastMessage("hello"));
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }
        await Task.Delay(100);

        Assert.Equal("hello", toast.LastMessage);
    }

    [Fact]
    public async Task Receive_InitializedMessage_navigates_home_and_checks_playback_modal()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var navigation = new RecordingNavigationService();
        var playbackModal = new RecordingPlaybackModalService();
        var sut = CreateSut(null!, navigation, playbackModal);

        sut.Receive(new InitializedMessage());
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }
        await Task.Delay(200);

        Assert.Equal(1, navigation.NavigateToHomeCalls);
        Assert.Equal(1, playbackModal.ShowOnWindowCreationCalls);
    }

    [Fact]
    public async Task Dispose_stops_delivering_toast_messages()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var toast = new RecordingToastService();
        var services = new ServiceCollection();
        services.AddSingleton<IToastService>(toast);
        var serviceProvider = services.BuildServiceProvider();
        var sut = CreateSut(serviceProvider, new UnusedNavigationServiceStub(), new RecordingPlaybackModalService());
        sut.RegisterMessageHandlers();
        sut.Dispose();

        WeakReferenceMessenger.Default.Send(new ShowToastMessage("after-dispose"));
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }
        await Task.Delay(100);

        Assert.Null(toast.LastMessage);
    }
}
