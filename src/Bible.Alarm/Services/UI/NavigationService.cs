using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class NavigationService(
    IServiceProvider serviceProvider,
    ILogger logger)
    : INavigationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger;

    private INavigation GetNavigation()
    {
        return _serviceProvider.GetRequiredService<INavigation>();
    }

    public async Task NavigateToHomeAsync()
    {
        // Always create a NEW Home page via DI (never reuse)
        var homePage = _serviceProvider.GetRequiredService<Home>();
        await PushFreshPageAsync(homePage, hasNavigationBar: false);
        
        // Ensure back button is hidden for Home page (it's effectively the root after BootstrapPage)
        NavigationPage.SetHasBackButton(homePage, false);
    }

    public async Task NavigateToScheduleAsync()
    {
        var page = _serviceProvider.GetRequiredService<Schedule>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<MusicSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<SongBookSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<TrackSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BibleSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BookSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<ChapterSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        ContentPage modal;
        
        // Use the appropriate modal based on the ViewModel type for compiled bindings
        if (bindingContext is BibleSelectionViewModel)
        {
            modal = _serviceProvider.GetRequiredService<BibleLanguageModal>();
        }
        else if (bindingContext is SongBookSelectionViewModel)
        {
            modal = _serviceProvider.GetRequiredService<MusicLanguageModal>();
        }
        else
        {
            throw new ArgumentException($"Unsupported ViewModel type: {bindingContext?.GetType().Name}", nameof(bindingContext));
        }
        
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenAlarmModalAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var navigation = GetNavigation();
                
                // Check if modal is already shown
                var existingModal = navigation.ModalStack.LastOrDefault();
                if (existingModal?.GetType() == typeof(AlarmModal) || 
                    (existingModal is NavigationPage navPage && navPage.CurrentPage is AlarmModal))
                {
                    return;
                }


                var modal = _serviceProvider.GetRequiredService<AlarmModal>();
                
                // Ensure modal is properly configured
                NavigationPage.SetHasNavigationBar(modal, false);
                
                // Push modal directly - wrapping in NavigationPage on Windows causes display issues
                await navigation.PushModalAsync(modal);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - use a logger if available
                System.Diagnostics.Debug.WriteLine($"Error opening AlarmModal: {ex.Message}");
            }
        });
    }


    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task PopModalAsync()
    { 
            var navigation = GetNavigation();
            if (navigation.ModalStack.Count > 0)
            {
                var modal = navigation.ModalStack.LastOrDefault();
                await navigation.PopModalAsync();
                
                // Dispose the modal - handle both direct modals and wrapped modals
                if (modal is NavigationPage navPage && navPage.CurrentPage is IDisposable disposablePage)
                {
                    disposablePage.Dispose();
                }
                else if (modal is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
    }

    public async Task PopAsync()
    {
        var navigation = GetNavigation();
        if (navigation.NavigationStack.Count > 1)
        {
            var page = navigation.NavigationStack.LastOrDefault();
            await navigation.PopAsync();
            if (page is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Pushes a fresh page instance, then clears all other pages from the stack, leaving only the newly pushed page.
    /// </summary>
    private async Task PushFreshPageAsync<T>(T page, bool hasNavigationBar = true) where T : Page
    {
        var navigation = GetNavigation();

        // Set navigation bar setting
        NavigationPage.SetHasNavigationBar(page, hasNavigationBar);

        // Push the fresh page first
        await navigation.PushAsync(page);
    }

    public MediaElement GetMediaElement()
    {
        // Find MediaElement from BootstrapPage (always on navigation stack)
        var navigation = GetNavigation();
        
        // BootstrapPage is always the first page in the navigation stack
        var navStack = navigation.NavigationStack;
        foreach (var page in navStack)
        {
            if (page is BootstrapPage bootstrapPage)
            {
                var mediaElement = bootstrapPage.FindByName("MediaPlayer") as MediaElement;
                if (mediaElement != null)
                {
                    _logger.Information("MediaElement found in BootstrapPage");
                    return mediaElement;
                }
            }
        }

        _logger.Warning("MediaElement not found in BootstrapPage");
        // Return a temporary instance (shouldn't happen if BootstrapPage is loaded)
        return new MediaElement
        {
            ShouldAutoPlay = false,
            ShouldLoopPlayback = false,
            ShouldShowPlaybackControls = false
        };
    }
}

