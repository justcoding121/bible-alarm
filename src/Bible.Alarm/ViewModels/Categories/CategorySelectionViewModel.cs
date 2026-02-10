#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Messages;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Categories;

public sealed class CategorySelectionViewModel : ObservableObject, IListViewModel, IDisposable, IRecipient<CategoryFetchProgressMessage>
{
    private readonly ICategoryService categoryService;
    private readonly INavigationService navigationService;
    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private bool isBusy = true;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private CategoryListViewItemModel? selectedCategory;
    private CategoryListViewItemModel? currentFetchingCategory;
    private volatile bool fetchErrorReceived;

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
        
        // Subscribe to progress messages from the effect handler
        WeakReferenceMessenger.Default.Register(this);
        
        _ = LoadCategoriesAsync();
    }

    public void Receive(CategoryFetchProgressMessage message)
    {
        var progress = message.Value;

        if (progress.HasError && currentFetchingCategory != null && currentFetchingCategory.Id == progress.CategoryId)
        {
            fetchErrorReceived = true;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                currentFetchingCategory.DownloadProgress = 0;
                var toastService = ServiceProviderManager.GetService<IToastService>();
                await (toastService?.ShowMessage("Please check your internet connection.") ?? Task.CompletedTask);
                await navigationService.PopModalAsync();
            });
            return;
        }

        if (currentFetchingCategory != null && currentFetchingCategory.Id == progress.CategoryId)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                currentFetchingCategory.DownloadProgress = progress.Progress;
            });
        }
    }

    public ICommand SelectCategoryCommand => new AsyncRelayCommand<CategoryListViewItemModel>(async (category) =>
    {
        if (category == null)
        {
            return;
        }

        // Get current state before category change to detect actual change
        var currentSchedule = state.Value.CurrentSchedule;
        var previousLanguageCode = currentSchedule?.BiblePublicationLanguageCode;
        var previousCategoryName = currentSchedule?.BiblePublicationCategoryName;
        var previousPublicationCode = currentSchedule?.BiblePublicationCode;

        fetchErrorReceived = false;
        currentFetchingCategory = category;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            category.IsNavigating = true;
        });

        try
        {
            dispatcher.Dispatch(new CategorySelectionAction(category.Id, category.Name, previousLanguageCode));

            const int maxWaitAttempts = 60;
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                if (fetchErrorReceived)
                    break;

                var currentState = state.Value.CurrentSchedule;
                if (currentState != null &&
                    currentState.BiblePublicationCategoryName == category.Name &&
                    !string.IsNullOrEmpty(currentState.BiblePublicationCode) &&
                    !string.IsNullOrWhiteSpace(currentState.BiblePublicationTrackCode) &&
                    (currentState.BiblePublicationCategoryName != previousCategoryName ||
                     currentState.BiblePublicationCode != previousPublicationCode))
                {
                    break;
                }

                await Task.Delay(delayMs);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "CategorySelectionViewModel: Error during category selection for category={CategoryName}", category.Name);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                category.IsNavigating = false;
            });
            currentFetchingCategory = null;
        }

        if (!fetchErrorReceived)
        {
            try
            {
                await navigationService.PopModalAsync();
                Serilog.Log.Debug("CategorySelectionViewModel: Modal closed successfully for category={CategoryName}", category.Name);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "CategorySelectionViewModel: Error closing modal for category={CategoryName}", category.Name);
            }
        }
    });

    private async Task LoadCategoriesAsync()
    {
        // Note: IsBusy defaults to true. Do NOT set it to false here - the modal controls this via ModalScrollHelper
        try
        {
            var categories = await categoryService.GetAllCategoriesAsync();
            var categoryVMs = categories.Select(c => new CategoryListViewItemModel(c)).ToList();
            
            // Get current category from state to mark as selected
            var currentCategoryName = state.Value.CurrentSchedule?.BiblePublicationCategoryName;
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var categoryVM in categoryVMs)
                {
                    // Mark the current category as selected for scroll-to-selected
                    if (!string.IsNullOrEmpty(currentCategoryName) && 
                        string.Equals(categoryVM.Name, currentCategoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        categoryVM.IsSelected = true;
                        SelectedCategory = categoryVM;
                        Serilog.Log.Debug("LoadCategoriesAsync: Marked category {CategoryName} as selected", categoryVM.Name);
                    }
                    Categories.Add(categoryVM);
                }
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error loading categories");
        }
        // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
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
        WeakReferenceMessenger.Default.Unregister<CategoryFetchProgressMessage>(this);
    }
}
