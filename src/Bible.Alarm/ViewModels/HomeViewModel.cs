using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Database;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Database.Migrations;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Views.Schedule;

namespace Bible.Alarm.ViewModels;

public class HomeViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IToastService _popUpService;
    private readonly IAlarmService _alarmService;

    private readonly INotificationService _notificationService;

    private readonly Dictionary<ScheduleListItem, PropertyChangedEventHandler> _isEnabledHandlers = [];
    // Map AlarmSchedule IDs to ScheduleListItem ViewModels for UI binding
    private readonly Dictionary<long, ScheduleListItem> _scheduleViewModels = [];

    private readonly Func<AlarmSchedule, ScheduleListItem> _scheduleListItemFactory;

    private readonly Fluxor.IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    public HomeViewModel(
        ILogger logger,
        IToastService popUpService, 
        INavigation navigation,
        IMediaCacheService mediaCacheService,
        IAlarmService alarmService,
        INotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        Func<AlarmSchedule, ScheduleListItem> scheduleListItemFactory,
        Fluxor.IDispatcher dispatcher,
        IState<ApplicationState> state)
    {
        _logger = logger;
        _popUpService = popUpService;
        var navigation1 = navigation;
        _alarmService = alarmService;
        _notificationService = notificationService;
        _scopeFactory = scopeFactory;
        _scheduleListItemFactory = scheduleListItemFactory;
        _dispatcher = dispatcher;
        _state = state;

        AddScheduleCommand = new AsyncRelayCommand(async () =>
        {
            _dispatcher.Dispatch(new ViewScheduleAction(null));
            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ScheduleViewModel>();
            var page = scope.ServiceProvider.GetRequiredService<Schedule>();
            page.BindingContext = viewModel;
            await navigation1.PushAsync(page);
        });

        ViewScheduleCommand = new AsyncRelayCommand<ScheduleListItem>(async x =>
        {
            x.Schedule.IsEnabled = x.IsEnabled;

            _dispatcher.Dispatch(new ViewScheduleAction(x.Schedule));

            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ScheduleViewModel>();
            var page = scope.ServiceProvider.GetRequiredService<Schedule>();
            page.BindingContext = viewModel;
            await navigation1.PushAsync(page);
        });


        //set schedules from initial state.
        //this should fire only once (look at the where condition).
        ObservableHashSet<AlarmSchedule> lastSchedules = null;

        _state.StateChanged += (sender, e) =>
        {
            var stateValue = _state.Value;
            if (stateValue.Schedules == null || stateValue.Schedules == lastSchedules) return;
            UpdateScheduleViewModels(stateValue.Schedules);
            lastSchedules = stateValue.Schedules;
            ListenIsEnabledChanges();
            IsBusy = false;
        };
        
        // Trigger initial update if state already has schedules
        if (_state.Value.Schedules == null) return;

        UpdateScheduleViewModels(_state.Value.Schedules);
        lastSchedules = _state.Value.Schedules;
        ListenIsEnabledChanges();
        IsBusy = false;

    }

    /// <summary>
    /// Initializes the HomeViewModel after bootstrap is complete.
    /// This should be called from App.xaml.cs after InitializedMessage is received.
    /// </summary>
    public async Task InitializeAsync()
    {
        await HandleInitialized();
    }

    private async Task HandleInitialized()
    {
        await _lock.WaitAsync();

        try
        {
            if (!_initialized)
            {
                await SeedDefaultAlarm();

                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

                var alarmSchedules = await scheduleDbContext.AlarmSchedules
                    .Include(x => x.BibleReadingSchedule)
                    .Include(x => x.Music)
                    .ToListAsync();

                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    //bible gateway is not supported anymore due to copyright issues
                    var toRemove = alarmSchedules.Where(x =>
                        BgSourceHelper.PublicationCodeToNameMappings.Any(y =>
                            y.Key == x.BibleReadingSchedule.PublicationCode)).ToList();

                    if (toRemove.Count != 0)
                    {
                        foreach (var item in toRemove)
                        {
                            item.BibleReadingSchedule.PublicationCode = "bi12";
                            item.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                        }

                        await scheduleDbContext.SaveChangesAsync();
                    }
                }

                var initialSchedules = new ObservableHashSet<AlarmSchedule>();
                foreach (var schedule in alarmSchedules)
                    initialSchedules.Add(schedule);

                _dispatcher.Dispatch(new InitializeAction(initialSchedules));

                _initialized = true;
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened in HomeViewModel Initialize.");
        }
        finally
        {
            try
            {
                _lock.Release();
            }
            catch (ObjectDisposedException e)
            {
                _logger.Error(e, "HomeViewModel: @lock disposed error.");
            }
        }
    }

    private async Task SeedDefaultAlarm()
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        if (!await scheduleDbContext.AlarmSchedules.AnyAsync()
            && !await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == AppConstants.GeneralSettingsKeys.AlarmSeeded)
            //for existing apps before version 1.30
            && !await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == AppConstants.GeneralSettingsKeys.AndroidBatteryOptimizationExclusionPromptShown))
        {
            var schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

            await scheduleDbContext.AlarmSchedules.AddAsync(schedule);
            await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
            {
                Key = AppConstants.GeneralSettingsKeys.AlarmSeeded,
                Value = "True"
            });

            await scheduleDbContext.SaveChangesAsync();
        }
    }

    private ObservableHashSet<ScheduleListItem> _schedules;

    public ObservableHashSet<ScheduleListItem> Schedules
    {
        get => _schedules;
        set => SetProperty(ref _schedules, value);
    }

    private void UpdateScheduleViewModels(ObservableHashSet<AlarmSchedule> schedules)
    {
        var newViewModels = new ObservableHashSet<ScheduleListItem>();
        var currentViewModelIds = new HashSet<long>();

        // Create or update ViewModels for each schedule
        foreach (var schedule in schedules)
        {
            currentViewModelIds.Add(schedule.Id);
            
            if (_scheduleViewModels.TryGetValue(schedule.Id, out var existingViewModel))
            {
                // Update existing ViewModel's Schedule property
                existingViewModel.Initialize(schedule);
                existingViewModel.IsEnabled = schedule.IsEnabled;
                newViewModels.Add(existingViewModel);
            }
            else
            {
                // Create new ViewModel using factory with full DI
                var viewModel = _scheduleListItemFactory(schedule);
                _scheduleViewModels[schedule.Id] = viewModel;
                newViewModels.Add(viewModel);
            }
        }

        // Dispose and remove ViewModels for schedules that no longer exist
        var toRemove = _scheduleViewModels.Keys.Where(id => !currentViewModelIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (!_scheduleViewModels.TryGetValue(id, out var viewModel)) continue;
            UnsubscribeFromIsEnabledChanges(viewModel);
            viewModel.Dispose();
            _scheduleViewModels.Remove(id);
        }

        Schedules = newViewModels;
    }

    private bool _isBusy = true;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            Loaded = !_isBusy;
        }
    }


    private bool _loaded = false;

    public bool Loaded
    {
        get => _loaded;
        set => SetProperty(ref _loaded, value);
    }

    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }

    private ScheduleViewModel _selectedSchedule;

    public ScheduleViewModel SelectedSchedule
    {
        get => _selectedSchedule;
        set => SetProperty(ref _selectedSchedule, value);
    }

    private bool _initialized = false;
    private readonly SemaphoreSlim _lock = new(1);

    private void ListenIsEnabledChanges()
    {
        // Subscribe to existing schedules
        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                SubscribeToIsEnabledChanges(item);
            }
        }

        // Subscribe to collection changes
        if (Schedules != null)
        {
            Schedules.CollectionChanged += (sender, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (ScheduleListItem item in e.NewItems)
                    {
                        SubscribeToIsEnabledChanges(item);
                    }
                }

                if (e.OldItems == null) return;

                foreach (ScheduleListItem item in e.OldItems)
                {
                    UnsubscribeFromIsEnabledChanges(item);
                }
            };
        }
    }

    private void SubscribeToIsEnabledChanges(ScheduleListItem item)
    {
        PropertyChangedEventHandler handler = async (sender, e) =>
        {
            if (e.PropertyName == "IsEnabled" && sender is ScheduleListItem scheduleItem)
            {
                await HandleIsEnabledChanged(scheduleItem);
            }
        };

        item.PropertyChanged += handler;
        _isEnabledHandlers[item] = handler;
    }

    private void UnsubscribeFromIsEnabledChanges(ScheduleListItem item)
    {
        if (!_isEnabledHandlers.TryGetValue(item, out var handler)) return;

        item.PropertyChanged -= handler;
        _isEnabledHandlers.Remove(item);
    }

    private async Task HandleIsEnabledChanged(ScheduleListItem scheduleItem)
    {
        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);

        if (scheduleItem.IsEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS
             || DeviceInfo.Platform == DevicePlatform.WinUI)
            && !await _notificationService.CanSchedule())
        {
            scheduleItem.IsEnabled = false;

            if (DeviceInfo.Platform == DevicePlatform.iOS)
                await _popUpService.ShowMessage(
                    "Cannot schedule alarm because you've disabled notifications. " +
                    "Please enable notification for this app under system settings.", 7);
            else
                await _popUpService.ShowMessage(
                    "Cannot schedule alarm because you've denied background apps permission. " +
                    "Please grant background apps permission for this app under system settings.", 7);

            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            return;
        }

        await Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            await using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            var existing = await scheduleDbContext.AlarmSchedules.FirstAsync(x => x.Id == scheduleItem.ScheduleId);
            existing.IsEnabled = scheduleItem.IsEnabled;
            await scheduleDbContext.SaveChangesAsync();

            _alarmService.Update(existing);
        });

        if (scheduleItem.IsEnabled) await _popUpService.ShowScheduledNotification(scheduleItem.Schedule);

        SetupMediaCache(scheduleItem.Schedule.Id);

        scheduleItem.RaisePropertiesChangedEvent();

        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
    }

    private void SetupMediaCache(long scheduleId)
    {
        Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                using var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
                await mediaCacheService.SetupAlarmCache(scheduleId);
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened in SetupAlarmCache task.");
            }
        });
    }

    public void Dispose()
    {
        // Unsubscribe from all schedule IsEnabled changes
        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                UnsubscribeFromIsEnabledChanges(item);
            }
        }

        _isEnabledHandlers.Clear();

        _lock.Dispose();
    }
}