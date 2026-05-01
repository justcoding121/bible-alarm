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

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            new ListModalAppearOptions(
                BusyOverlay,
                sectionCollectionView,
                GetSelectedItem: () => ViewModel?.SelectedSection,
                RefreshAction: ViewModel != null ? async () => await ViewModel.RefreshFromState() : null,
                GetItemCountFromViewModel: vm => (vm as MusicSectionSelectionViewModel)?.Sections?.Count ?? 0,
                OnFetchFailed: async (errorMessage) =>
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
                CancellationToken: cancellationTokenSource.Token));
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
        }

        isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
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
            if (ViewModel is null ||
                ViewModel.TrackSelectionCommand is not IAsyncRelayCommand<BiblePublicationSectionListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(sectionItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(sectionItem);
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
