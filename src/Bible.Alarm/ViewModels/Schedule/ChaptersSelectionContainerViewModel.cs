using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class ChaptersSelectionContainerViewModel : ObservableObject
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;

    private int scheduleId;
    private AlarmSchedule? model;
    private bool notificationEnabled;
    private bool alwaysPlayFromStart;

    private ObservableCollection<NumberOfChaptersListViewItemModel> numberOfChaptersList = new();
    private NumberOfChaptersListViewItemModel? currentNumberOfChapters;

    public ChaptersSelectionContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.OpenNumberOfChaptersModalAsync(this);
        });

        SelectNumberOfChaptersCommand = new AsyncRelayCommand<NumberOfChaptersListViewItemModel>(async x =>
        {
            if (CurrentNumberOfChapters != null)
            {
                CurrentNumberOfChapters.IsSelected = false;
            }

            CurrentNumberOfChapters = x;
            CurrentNumberOfChapters.IsSelected = true;

            // Update the model immediately so the UI reflects the change
            if (model != null && CurrentNumberOfChapters != null)
            {
                model.NumberOfChaptersToRead = CurrentNumberOfChapters.Value;
            }

            // Explicitly notify property changes to ensure UI binding updates
            OnPropertyChanged(nameof(CurrentNumberOfChapters));
            OnPropertyChanged(nameof(CurrentNumberOfChaptersText));

            await navigationService.PopModalAsync();
        });

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        BatteryOptimizationExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    await MarkBatteryOptimizationModalAsShown();
                    await navigationService.PopModalAsync();
                    batteryService.ShowOptimizationSettingsPage();
                }
            }
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();
            await navigationService.PopModalAsync();
        });
    }

    public void Initialize(int scheduleId, AlarmSchedule model, bool notificationEnabled, bool alwaysPlayFromStart)
    {
        this.scheduleId = scheduleId;
        this.model = model;
        this.notificationEnabled = notificationEnabled;
        this.alwaysPlayFromStart = alwaysPlayFromStart;

        PopulateNumberOfChaptersListView(model);
        
        OnPropertyChanged(nameof(NotificationEnabled));
        OnPropertyChanged(nameof(AlwaysPlayFromStart));
    }

    public ICommand OpenModalCommand { get; private set; } = null!;
    public ICommand SelectNumberOfChaptersCommand { get; private set; } = null!;
    public ICommand ToggleAlwaysPlayFromStartCommand { get; private set; } = null!;
    public ICommand NotificationEnabledCommand { get; private set; } = null!;
    public ICommand CloseModalCommand { get; private set; } = null!;

    public ObservableCollection<NumberOfChaptersListViewItemModel> NumberOfChaptersList
    {
        get => numberOfChaptersList;
        set => SetProperty(ref numberOfChaptersList, value);
    }

    public NumberOfChaptersListViewItemModel? CurrentNumberOfChapters
    {
        get => currentNumberOfChapters;
        set
        {
            if (SetProperty(ref currentNumberOfChapters, value))
            {
                // Notify that the Text property (computed from CurrentNumberOfChapters) has changed
                OnPropertyChanged(nameof(CurrentNumberOfChaptersText));
            }
        }
    }

    /// <summary>
    /// Computed property for binding to the number of chapters text in the UI.
    /// This ensures the UI updates when CurrentNumberOfChapters changes.
    /// </summary>
    public string CurrentNumberOfChaptersText => CurrentNumberOfChapters?.Text ?? string.Empty;

    public bool NotificationEnabled
    {
        get => notificationEnabled;
        set
        {
            if (!value)
            {
                _ = ShowBatteryOptimizationExclusionPage();
            }

            if (SetProperty(ref notificationEnabled, value) && model != null)
            {
                model.NotificationEnabled = value;
            }
        }
    }

    private bool canOptimizeBattery;

    public bool CanOptimizeBattery
    {
        get => canOptimizeBattery;
        set => SetProperty(ref canOptimizeBattery, value);
    }

    public ICommand BatteryOptimizationExcludeCommand { get; private set; } = null!;
    public ICommand BatteryOptimizationDismissCommand { get; private set; } = null!;

    private async Task ShowBatteryOptimizationExclusionPage()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
        if (batteryService == null)
        {
            return;
        }

        if (batteryService.CanShowOptimizeActivity())
        {
            CanOptimizeBattery = true;
        }

        if (await batteryService.ShouldShowModalAsync())
        {
            await navigationService.OpenBatteryOptimizationModalAsync(this);
        }
        else
        {
            // If modal was already shown, just show the settings page directly
            batteryService.ShowOptimizationSettingsPage();
        }
    }

    private async Task MarkBatteryOptimizationModalAsShown()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                await batteryService.MarkModalAsShownAsync();
            }
        }
    }

    public bool AlwaysPlayFromStart
    {
        get => alwaysPlayFromStart;
        set
        {
            if (SetProperty(ref alwaysPlayFromStart, value) && model != null)
            {
                model.AlwaysPlayFromStart = value;
            }
        }
    }

    private void PopulateNumberOfChaptersListView(AlarmSchedule model)
    {
        // Preserve the current selection if user has made one (to prevent SetModel from overwriting user's choice)
        var preservedSelection = CurrentNumberOfChapters?.Value;

        var chapterVMs = new ObservableCollection<NumberOfChaptersListViewItemModel>();

        for (var i = 1; i <= 21; i++)
        {
            var chaptersVm = new NumberOfChaptersListViewItemModel(i);

            // If user has made a selection, use that; otherwise use the model's value
            var shouldSelect = preservedSelection.HasValue
                ? preservedSelection.Value == i
                : model.NumberOfChaptersToRead == i;

            if (shouldSelect)
            {
                chaptersVm.IsSelected = true;
                CurrentNumberOfChapters = chaptersVm;
            }

            chapterVMs.Add(chaptersVm);
        }

        NumberOfChaptersList = chapterVMs;
    }
}

