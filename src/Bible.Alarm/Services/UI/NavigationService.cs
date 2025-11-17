using Bible.Alarm.Common.Interfaces.UI;
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
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<LanguageModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenAlarmModalAsync()
    {
        var navigation = GetNavigation();
        if (navigation.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal))
        {
            return;
        }

        var modal = _serviceProvider.GetRequiredService<AlarmModal>();
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenMediaProgressModalAsync()
    {
        var navigation = GetNavigation();
        if (navigation.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal))
        {
            return;
        }

        var modal = _serviceProvider.GetRequiredService<MediaProgressModal>();
        await navigation.PushModalAsync(modal);
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
            await navigation.PopModalAsync();
        }
    }
}

