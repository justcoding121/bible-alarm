#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Categories;

public sealed class CategorySelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly ICategoryService categoryService;
    private readonly INavigationService navigationService;
    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private bool isBusy = true;
    private CategoryListViewItemModel? selectedCategory;

    public CategorySelectionViewModel(
        ICategoryService categoryService,
        INavigationService navigationService,
        IDispatcher dispatcher,
        IState<ApplicationState> state)
    {
        this.categoryService = categoryService;
        this.navigationService = navigationService;
        this.dispatcher = dispatcher;
        this.state = state;
        CloseModalCommand = new AsyncRelayCommand(async () => await navigationService.PopModalAsync());
        _ = LoadCategoriesAsync();
    }

    public ICommand SelectCategoryCommand => new AsyncRelayCommand<CategoryListViewItemModel>(async (category) =>
    {
        if (category == null)
        {
            return;
        }

        // Get current language code before category change to preserve it if possible
        var currentSchedule = state.Value.CurrentSchedule;
        var previousLanguageCode = currentSchedule?.BiblePublicationLanguageCode;

        // Dispatch action to update state, including previous language code
        dispatcher.Dispatch(new CategorySelectionAction(category.Id, category.Name, previousLanguageCode));
        
        // Close modal
        await navigationService.PopModalAsync();
    });

    private async Task LoadCategoriesAsync()
    {
        IsBusy = true;
        try
        {
            var categories = await categoryService.GetAllCategoriesAsync();
            var categoryVMs = categories.Select(c => new CategoryListViewItemModel(c)).ToList();
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var categoryVM in categoryVMs)
                {
                    Categories.Add(categoryVM);
                }
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public ObservableCollection<CategoryListViewItemModel> Categories { get; } = [];

    public CategoryListViewItemModel? SelectedCategory
    {
        get => selectedCategory;
        set => SetProperty(ref selectedCategory, value);
    }

    public object? SelectedItem
    {
        get => selectedCategory;
    }

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ICommand CloseModalCommand { get; private set; } = null!;

    public void Dispose()
    {
        // No resources to dispose
    }
}
