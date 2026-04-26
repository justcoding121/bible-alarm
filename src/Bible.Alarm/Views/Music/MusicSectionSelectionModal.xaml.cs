#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicSectionSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingSection;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public MusicSectionSelectionViewModel? ViewModel => BindingContext as MusicSectionSelectionViewModel;

    public MusicSectionSelectionModal()
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
            sectionCollectionView,
            getSelectedItem: () => ViewModel?.SelectedSection,
            refreshAction: ViewModel != null ? async () => await ViewModel.RefreshFromState() : null,
            getItemCountFromViewModel: vm => (vm as MusicSectionSelectionViewModel)?.Sections?.Count ?? 0,
            onFetchFailed: async (errorMessage) =>
            {
                await this.Dispatcher.DispatchAsync(async () =>
                {
                    try
                    {
                        await Task.Delay(500);
                        await navigationService.PopModalAsync();
                    }
                    catch (InvalidOperationException ex)
                    {
                        Log.Logger.Debug(ex, "MusicSectionSelectionModal: PopModalAsync failed (modal may already be closed or platform stack out of sync)");
                    }
                });
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

    private async void OnSectionItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not BiblePublicationSectionListViewItemModel sectionItem)
        {
            return;
        }

        await SafeTeardown.CancelAsyncNoThrow(cancellationTokenSource);

        if (isSelectingSection)
        {
            return;
        }

        isSelectingSection = true;

        sectionItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel != null && ViewModel.TrackSelectionCommand is IAsyncRelayCommand<BiblePublicationSectionListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(sectionItem))
                {
                    await asyncCommand.ExecuteAsync(sectionItem);
                }
            }
        }
        catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
        {
            await Task.Delay(500);
            await navigationService.PopModalAsync();
            await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
        }
        finally
        {
            sectionItem.IsNavigating = false;
            isSelectingSection = false;
        }
    }
}
