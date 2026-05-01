#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Categories;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class CategorySelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingCategory;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public CategorySelectionModal()
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

        var categoryViewModel = ViewModel as CategorySelectionViewModel;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            new ListModalAppearOptions(
                BusyOverlay,
                CategoryCollectionView,
                GetSelectedItem: () => categoryViewModel?.Categories?.FirstOrDefault(c => c.IsSelected),
                OnFetchFailed: async (errorMessage) =>
                {
                    await navigationService.PopModalAsync();
                    await toastService.ShowMessage(errorMessage);
                },
                CancellationToken: cancellationTokenSource.Token));
    }

    private async void OnCategoryItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not CategoryListViewItemModel categoryItem)
        {
            return;
        }

        await SafeTeardown.CancelAsyncNoThrow(cancellationTokenSource);

        if (isSelectingCategory)
        {
            return;
        }

        isSelectingCategory = true;

        categoryItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel is CategorySelectionViewModel categoryViewModel
                && categoryViewModel.SelectCategoryCommand is IAsyncRelayCommand<CategoryListViewItemModel> asyncCommand
                && asyncCommand.CanExecute(categoryItem))
            {
                await asyncCommand.ExecuteAsync(categoryItem);
            }
        }
        catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
        {
            await navigationService.PopModalAsync();
            await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
        }
        finally
        {
            categoryItem.IsNavigating = false;
            isSelectingCategory = false;
        }
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
}
