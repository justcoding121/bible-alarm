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
    IServiceProvider serviceProvider)
    : INavigationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private INavigation GetNavigation()
    {
        return _serviceProvider.GetRequiredService<INavigation>();
    }

    public async Task NavigateToHomeAsync()
    {
        var navigation = GetNavigation();
        var homePage = _serviceProvider.GetRequiredService<Home>();
        NavigationPage.SetHasNavigationBar(homePage, false);
        await navigation.PushAsync(homePage);
        
        // Remove any other pages from the stack
        var pagesToRemove = navigation.NavigationStack.Where(p => p != homePage).ToList();
        foreach (var page in pagesToRemove)
        {
            navigation.RemovePage(page);
        }
    }

    public async Task NavigateToScheduleAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<Schedule>();
        await navigation.PushAsync(page);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<MusicSelection>();
        await navigation.PushAsync(page);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<SongBookSelection>();
        await navigation.PushAsync(page);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<TrackSelection>();
        await navigation.PushAsync(page);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<BibleSelection>();
        await navigation.PushAsync(page);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<BookSelection>();
        await navigation.PushAsync(page);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        var navigation = GetNavigation();
        var page = _serviceProvider.GetRequiredService<ChapterSelection>();
        await navigation.PushAsync(page);
    }

    public async Task PopAsync()
    {
        var navigation = GetNavigation();
        if (navigation.NavigationStack.Count > 1)
        {
            await navigation.PopAsync();
        }
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
        // Check if alarm modal is already open
        if (navigation.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal))
        {
            return;
        }

        var vm = _serviceProvider.GetRequiredService<AlarmViewModal>();
        var modal = _serviceProvider.GetRequiredService<AlarmModal>();
        modal.BindingContext = vm;
        await navigation.PushModalAsync(modal);
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
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task CloseModalAsync()
    {
        var navigation = GetNavigation();
        if (navigation.ModalStack.Count > 0)
        {
            var modal = await navigation.PopModalAsync();
            if (modal.BindingContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

