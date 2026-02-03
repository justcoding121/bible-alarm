#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicTypeSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public MusicTypeSelectionViewModel? ViewModel => BindingContext as MusicTypeSelectionViewModel;

    public MusicTypeSelectionModal()
    {
        InitializeComponent();
        var services = Application.Current!.Handler!.MauiContext!.Services;
        navigationService = services.GetRequiredService<INavigationService>();
        toastService = services.GetRequiredService<IToastService>();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var result = await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            musicTypesCollectionView,
            getSelectedItem: () => ViewModel?.SelectedMusicType,
            refreshAction: () =>
            {
                ViewModel?.RefreshFromState();
                return Task.CompletedTask;
            },
            onFetchFailed: async (errorMessage) =>
            {
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(errorMessage);
            },
            cancellationToken: cancellationTokenSource.Token);

        if (result == ModalAppearingResult.FetchFailed)
        {
            return;
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
            isDisposed = true;
        }
    }

    private async void OnMusicTypeItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is MusicTypeListItemViewModel musicTypeItem)
        {
            // Set IsNavigating immediately to show progress indicator
            musicTypeItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.MusicPublicationSelectionCommand is IAsyncRelayCommand<MusicTypeListItemViewModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(musicTypeItem))
                    {
                        await asyncCommand.ExecuteAsync(musicTypeItem);
                    }
                }
            }
            catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
            {
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
            }
            finally
            {
                // Reset IsNavigating after operation completes
                musicTypeItem.IsNavigating = false;
            }
        }
    }
}
