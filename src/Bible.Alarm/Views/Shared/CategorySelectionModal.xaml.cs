#nullable enable
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
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public CategorySelectionModal()
    {
        InitializeComponent();
        navigationService = Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<INavigationService>();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var categoryViewModel = ViewModel as CategorySelectionViewModel;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            CategoryCollectionView,
            getSelectedItem: () => categoryViewModel?.Categories?.FirstOrDefault(c => c.IsSelected),
            refreshAction: null,
            cancellationToken: cancellationTokenSource.Token);
    }

    private async void OnCategoryItemTapped(object? sender, TappedEventArgs e)
    {
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is CategoryListViewItemModel categoryItem)
        {
            if (ViewModel is CategorySelectionViewModel categoryViewModel)
            {
                // Mark as selected
                foreach (var category in categoryViewModel.Categories)
                {
                    category.IsSelected = category.Id == categoryItem.Id;
                }
                categoryViewModel.SelectedCategory = categoryItem;
                
                // Execute select command which dispatches action and closes modal
                if (categoryViewModel.SelectCategoryCommand is IAsyncRelayCommand<CategoryListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(categoryItem))
                    {
                        await asyncCommand.ExecuteAsync(categoryItem);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null);
            isDisposed = true;
        }
    }
}
