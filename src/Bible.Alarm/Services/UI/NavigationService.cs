using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;

namespace Bible.Alarm.Services.UI;

public class NavigationService(
    IServiceProvider serviceProvider)
    : INavigationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private INavigation GetNavigation()
    {
        return _serviceProvider.GetRequiredService<INavigation>();
    }


    /// <summary>
    /// Removes all pages from the navigation stack except the specified page.
    /// </summary>
    private void ClearNavigationStackExcept(Page pageToKeep)
    {
        var navigation = GetNavigation();
        var pagesToRemove = navigation.NavigationStack.Where(p => p != pageToKeep).ToList();
        foreach (var page in pagesToRemove)
        {
            navigation.RemovePage(page);
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
        
        // Then remove all other pages from the stack, leaving only the newly pushed page
        ClearNavigationStackExcept(page);
    }

    public async Task NavigateToHomeAsync()
    {
        // Always create a NEW Home page via DI (never reuse)
        var homePage = _serviceProvider.GetRequiredService<Home>();
        await PushFreshPageAsync(homePage, hasNavigationBar: false);
    }

    public async Task NavigateToScheduleAsync()
    {
        var page = _serviceProvider.GetRequiredService<Schedule>();
        await PushFreshPageAsync(page);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<MusicSelection>();
        await PushFreshPageAsync(page);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<SongBookSelection>();
        await PushFreshPageAsync(page);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<TrackSelection>();
        await PushFreshPageAsync(page);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BibleSelection>();
        await PushFreshPageAsync(page);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BookSelection>();
        await PushFreshPageAsync(page);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<ChapterSelection>();
        await PushFreshPageAsync(page);
    }


    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        
        // Push the modal first
        await navigation.PushModalAsync(modal);
        
        // Then clear all pages from the navigation stack, leaving only the modal
        ClearNavigationStackExcept(null);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<LanguageModal>();
        modal.BindingContext = bindingContext;
        
        // Push the modal first
        await navigation.PushModalAsync(modal);
        
        // Then clear all pages from the navigation stack, leaving only the modal
        ClearNavigationStackExcept(null);
    }

    public async Task OpenAlarmModalAsync()
    {
        var navigation = GetNavigation();
        // Check if alarm modal is already open
        if (navigation.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal))
        {
            return;
        }

        var vm = _serviceProvider.GetRequiredService<AlarmViewModal>();
        var modal = _serviceProvider.GetRequiredService<AlarmModal>();
        modal.BindingContext = vm;
        
        // Push the modal first
        await navigation.PushModalAsync(modal);
        
        // Then clear all pages from the navigation stack, leaving only the modal
        ClearNavigationStackExcept(null);
    }

    public async Task OpenMediaProgressModalAsync()
    {
        var navigation = GetNavigation();
        // Check if media progress modal is already open
        if (navigation.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal))
        {
            return;
        }

        var vm = _serviceProvider.GetRequiredService<MediaProgressViewModal>();
        var modal = _serviceProvider.GetRequiredService<MediaProgressModal>();
        modal.BindingContext = vm;
        
        // Push the modal first
        await navigation.PushModalAsync(modal);
        
        // Then clear all pages from the navigation stack, leaving only the modal
        ClearNavigationStackExcept(null);
    }

    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
        modal.BindingContext = bindingContext;
        
        // Push the modal first
        await navigation.PushModalAsync(modal);
        
        // Then clear all pages from the navigation stack, leaving only the modal
        ClearNavigationStackExcept(null);
    }

}

