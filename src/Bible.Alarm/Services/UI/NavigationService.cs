using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.UI;

public class NavigationService(
    INavigation navigation,
    IServiceScopeFactory scopeFactory)
    : INavigationService
{
    private readonly INavigation _navigation = navigation;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task NavigateToScheduleAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ScheduleViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<Schedule>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<MusicSelectionViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<MusicSelection>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<SongBookSelectionViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<SongBookSelection>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<TrackSelectionViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<TrackSelection>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<BibleSelectionViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<BibleSelection>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<BookSelectionViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<BookSelection>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ChapterSelectionViewModel>();
        var page = scope.ServiceProvider.GetRequiredService<ChapterSelection>();
        page.BindingContext = viewModel;
        await _navigation.PushAsync(page);
    }

    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        using var scope = _scopeFactory.CreateScope();
        var modal = scope.ServiceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        await _navigation.PushModalAsync(modal);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        using var scope = _scopeFactory.CreateScope();
        var modal = scope.ServiceProvider.GetRequiredService<LanguageModal>();
        modal.BindingContext = bindingContext;
        await _navigation.PushModalAsync(modal);
    }

    public async Task CloseModalAsync()
    {
        if (_navigation.ModalStack.Count > 0)
        {
            var modal = await _navigation.PopModalAsync();
            if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
        }
    }
}

