using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.UI;

public class NavigationService(
    INavigation navigation,
    IServiceProvider serviceProvider)
    : INavigationService
{
    private readonly INavigation _navigation = navigation;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task NavigateToHomeAsync()
    {
        var homePage = _serviceProvider.GetRequiredService<Home>();
        NavigationPage.SetHasNavigationBar(homePage, false);
        await _navigation.PushAsync(homePage);
        
        // Remove any other pages from the stack
        var pagesToRemove = _navigation.NavigationStack.Where(p => p != homePage).ToList();
        foreach (var page in pagesToRemove)
        {
            _navigation.RemovePage(page);
        }
    }

    public async Task NavigateToScheduleAsync()
    {
        var page = _serviceProvider.GetRequiredService<Schedule>();
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<MusicSelection>();
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<SongBookSelection>();
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<TrackSelection>();
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BibleSelection>();
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BookSelection>();
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<ChapterSelection>();
        await _navigation.PushAsync(page);
    }

    public async Task PopAsync()
    {
        if (_navigation.NavigationStack.Count > 1)
        {
            await _navigation.PopAsync();
        }
    }

    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        var modal = _serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        await _navigation.PushModalAsync(modal);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var modal = _serviceProvider.GetRequiredService<LanguageModal>();
        modal.BindingContext = bindingContext;
        await _navigation.PushModalAsync(modal);
    }

    public async Task OpenAlarmModalAsync()
    {
        // Check if alarm modal is already open
        if (_navigation.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal))
        {
            return;
        }

        var vm = _serviceProvider.GetRequiredService<AlarmViewModal>();
        var modal = _serviceProvider.GetRequiredService<AlarmModal>();
        modal.BindingContext = vm;
        await _navigation.PushModalAsync(modal);
    }

    public async Task OpenMediaProgressModalAsync()
    {
        // Check if media progress modal is already open
        if (_navigation.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal))
        {
            return;
        }

        var vm = _serviceProvider.GetRequiredService<MediaProgressViewModal>();
        var modal = _serviceProvider.GetRequiredService<MediaProgressModal>();
        modal.BindingContext = vm;
        await _navigation.PushModalAsync(modal);
    }

    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
        modal.BindingContext = bindingContext;
        await _navigation.PushModalAsync(modal);
    }

    public async Task CloseModalAsync()
    {
        if (_navigation.ModalStack.Count > 0)
        {
            var modal = await _navigation.PopModalAsync();
            if (modal.BindingContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

