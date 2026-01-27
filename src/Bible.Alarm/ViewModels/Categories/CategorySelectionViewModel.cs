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
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = string.Empty;
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

        // Show progress immediately on UI thread before any async work
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsBusy = true;
            ShowProgress = true;
            ProgressPercent = 0.0;
            ProgressText = "0%";
        });
        
        // Give UI thread a chance to render the progress
        await Task.Delay(50);

        try
        {
            // Dispatch action to update state, including previous language code
            dispatcher.Dispatch(new CategorySelectionAction(category.Id, category.Name, previousLanguageCode));
            
            // Wait for state to be updated (cascade effect)
            ProgressPercent = 0.3;
            ProgressText = "30%";
            
            const int maxWaitAttempts = 60; // Increased for longer fetches
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                var currentState = state.Value.CurrentSchedule;
                if (currentState != null && 
                    !string.IsNullOrEmpty(currentState.BiblePublicationCategoryName) &&
                    currentState.BiblePublicationCategoryName == category.Name &&
                    !string.IsNullOrEmpty(currentState.BiblePublicationCode) &&
                    currentState.BiblePublicationTrackNumber.HasValue &&
                    currentState.BiblePublicationTrackNumber.Value > 0)
                {
                    // Cascade complete
                    ProgressPercent = 1.0;
                    ProgressText = "100%";
                    await Task.Delay(200); // Brief delay to show completion
                    break;
                }
                
                // Update progress gradually
                double progress;
                if (i < 30)
                {
                    progress = 0.3 + (i / 30.0) * 0.5; // 0.3 to 0.8
                }
                else
                {
                    progress = 0.8 + ((i - 30) / 30.0) * 0.2; // 0.8 to 1.0
                }
                
                var percent = (int)Math.Round(progress * 100);
                ProgressPercent = progress;
                ProgressText = $"{percent}%";
                
                await Task.Delay(delayMs);
            }
        }
        finally
        {
            IsBusy = false;
            ShowProgress = false;
        }
        
        // Close modal after fetch completes
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

    public bool ShowProgress
    {
        get => showProgress;
        set => SetProperty(ref showProgress, value);
    }

    public double ProgressPercent
    {
        get => progressPercent;
        set => SetProperty(ref progressPercent, value);
    }

    public string ProgressText
    {
        get => progressText;
        set => SetProperty(ref progressText, value);
    }

    public ICommand CloseModalCommand { get; private set; } = null!;

    public void Dispose()
    {
        // No resources to dispose
    }
}
