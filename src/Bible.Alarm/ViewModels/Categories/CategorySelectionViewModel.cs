#nullable enable
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Messages.CategoryProgress;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Categories;

public sealed partial class CategorySelectionViewModel : ObservableObject, IListViewModel, IDisposable, IRecipient<CategoryFetchProgressMessage>
{
    private readonly ICategoryService categoryService;
    private readonly ICategoryNameService categoryNameService;
    private readonly INavigationService navigationService;
    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private bool isBusy = true;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private bool isCancelBusy;
    private CategoryListViewItemModel? selectedCategory;
    private CategoryListViewItemModel? currentFetchingCategory;
    private readonly CategoryFetchOutcomeLatch fetchOutcomeLatch = new();

    private sealed class CategoryFetchOutcomeLatch
    {
        public volatile bool FetchErrorReceived;
        public volatile bool CategorySelectionSucceededReceived;

        public void ResetForAttempt()
        {
            FetchErrorReceived = false;
            CategorySelectionSucceededReceived = false;
        }
    }

    public CategorySelectionViewModel(
        ICategoryService categoryService,
        ICategoryNameService categoryNameService,
        INavigationService navigationService,
        IDispatcher dispatcher,
        IState<ApplicationState> state)
    {
        this.categoryService = categoryService;
        this.categoryNameService = categoryNameService;
        this.navigationService = navigationService;
        this.dispatcher = dispatcher;
        this.state = state;
        CloseModalCommand = new AsyncRelayCommand(CloseModalAsync);
        CancelFetchCommand = new AsyncRelayCommand(CloseModalAsync);
        OverlayCancelCommand = new AsyncRelayCommand(OverlayCancelAsync);

        // Subscribe to progress messages from the effect handler
        WeakReferenceMessenger.Default.Register(this);
        
        _ = LoadCategoriesAsync();
    }

    public void Receive(CategoryFetchProgressMessage message)
    {
        var progress = message.Value;

        // Capture local reference - the field may be nulled by the polling loop's finally block.
        var fetchingCategory = currentFetchingCategory;

        if (progress.HasError && fetchingCategory != null && fetchingCategory.Id == progress.CategoryId)
        {
            fetchOutcomeLatch.FetchErrorReceived = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                fetchingCategory.DownloadProgress = 0;
            });
            return;
        }

        if (progress.IsComplete && !progress.HasError)
        {
            fetchOutcomeLatch.CategorySelectionSucceededReceived = true;
        }

        if (fetchingCategory != null && fetchingCategory.Id == progress.CategoryId &&
            progress.Progress >= 0.0 && progress.Progress <= 1.0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                fetchingCategory.DownloadProgress = progress.Progress;
            });
        }
    }

    public ICommand SelectCategoryCommand => new AsyncRelayCommand<CategoryListViewItemModel>(SelectCategoryAsync);

    private async Task SelectCategoryAsync(CategoryListViewItemModel? category)
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

        fetchOutcomeLatch.ResetForAttempt();
        currentFetchingCategory = category;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            category.IsNavigating = true;
        });

        try
        {
            var previousScheduleSnapshot = currentSchedule?.DeepClone();
            dispatcher.Dispatch(new CategorySelectionAction(category.Id, category.CategoryCode, previousLanguageCode, previousScheduleSnapshot));

            await WaitForCategoryCascadeApplyAsync(
                category,
                previousCategoryName,
                previousPublicationCode,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, AppConstants.Logging.CategorySelectionDiagnosticsLog.ErrorDuringCategorySelectionCategoryCode, category.CategoryCode);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                category.IsNavigating = false;
            });
            currentFetchingCategory = null;
        }

        await CloseModalAfterCategorySelectAsync(category);
    }

    private async Task WaitForCategoryCascadeApplyAsync(
        CategoryListViewItemModel category,
        string? previousCategoryName,
        string? previousPublicationCode,
        CancellationToken cancellationToken)
    {
        const int maxWaitAttempts = 60;
        const int delayMs = 200;
        for (var i = 0; i < maxWaitAttempts; i++)
        {
            if (fetchOutcomeLatch.FetchErrorReceived || fetchOutcomeLatch.CategorySelectionSucceededReceived)
                break;

            var currentState = state.Value.CurrentSchedule;
            if (currentState != null &&
                currentState.BiblePublicationCategoryName == category.CategoryCode &&
                !string.IsNullOrEmpty(currentState.BiblePublicationCode) &&
                !string.IsNullOrWhiteSpace(currentState.BiblePublicationTrackCode) &&
                (currentState.BiblePublicationCategoryName != previousCategoryName ||
                 currentState.BiblePublicationCode != previousPublicationCode))
            {
                break;
            }

            await Task.Delay(delayMs, cancellationToken);
        }
    }

    private async Task CloseModalAfterCategorySelectAsync(CategoryListViewItemModel category)
    {
        if (fetchOutcomeLatch.FetchErrorReceived)
        {
            try
            {
                await Task.Delay(500);
                await navigationService.PopModalAsync();
                var toastService = ServiceProviderManager.GetService<IToastService>();
                await (toastService?.ShowMessage(AppConstants.ToastMessages.PleaseCheckInternetConnection) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, AppConstants.Logging.CategorySelectionDiagnosticsLog.ErrorClosingModalAfterFetchErrorCategoryCode, category.CategoryCode);
            }
            return;
        }

        try
        {
            await navigationService.PopModalAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, AppConstants.Logging.CategorySelectionDiagnosticsLog.ErrorClosingModalCategoryCode, category.CategoryCode);
        }
    }

    private async Task LoadCategoriesAsync()
    {
        // Note: IsBusy defaults to true. Do NOT set it to false here - the modal controls this via ModalScrollHelper
        try
        {
            var categories = await categoryService.GetAllCategoriesAsync();
            var defaultLang = AppConstants.Media.DefaultLanguageCode;
            var categoryVMs = categories.Select(c =>
            {
                var displayName = categoryNameService.GetName(c.CategoryCode, defaultLang);
                return new CategoryListViewItemModel(c, displayName);
            }).ToList();

            // Get current category from state to mark as selected (state stores category code)
            var currentCategoryCode = state.Value.CurrentSchedule?.BiblePublicationCategoryName;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var categoryVM in categoryVMs)
                {
                    // Mark the current category as selected for scroll-to-selected
                    if (!string.IsNullOrEmpty(currentCategoryCode) &&
                        string.Equals(categoryVM.CategoryCode, currentCategoryCode, StringComparison.OrdinalIgnoreCase))
                    {
                        categoryVM.IsSelected = true;
                        SelectedCategory = categoryVM;
                        Serilog.Log.Debug(AppConstants.Logging.CategorySelectionDiagnosticsLog.LoadCategoriesAsyncMarkedCategorySelected, categoryVM.CategoryCode);
                    }
                    Categories.Add(categoryVM);
                }
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, AppConstants.Logging.CategorySelectionDiagnosticsLog.ErrorLoadingCategories);
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
        set
        {
            if (SetProperty(ref isBusy, value))
                OnPropertyChanged(nameof(ShowCancelButton));
        }
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

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    public bool ShowCancelButton => IsBusy;

    public ICommand CloseModalCommand { get; private set; }

    public ICommand CancelFetchCommand { get; private set; }

    public ICommand OverlayCancelCommand { get; private set; }

    private async Task CloseModalAsync()
    {
        await navigationService.PopModalAsync();
    }

    private async Task OverlayCancelAsync()
    {
        IsCancelBusy = true;
        await Task.Delay(50);
        try
        {
            await navigationService.PopModalAsync();
        }
        finally
        {
            IsCancelBusy = false;
        }
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<CategoryFetchProgressMessage>(this);
    }
}
