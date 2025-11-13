using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public class HomeViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IDatabaseSeedService _databaseSeedService;
    private readonly IScheduleMigrationService _scheduleMigrationService;

    private readonly Dictionary<int, ScheduleListItem> _scheduleViewModels = [];

    private readonly Func<AlarmSchedule, ScheduleListItem> _scheduleListItemFactory;

    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        Func<AlarmSchedule, ScheduleListItem> scheduleListItemFactory,
        IState<ApplicationState> state,
        IDatabaseSeedService databaseSeedService,
        IScheduleMigrationService scheduleMigrationService,
        INavigationService navigationService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _scheduleListItemFactory = scheduleListItemFactory;
        _state = state;
        _dispatcher = MauiAppHolder.Services.GetRequiredService<IDispatcher>();
        _databaseSeedService = databaseSeedService;
        _scheduleMigrationService = scheduleMigrationService;
        var navigationService1 = navigationService;

        AddScheduleCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService1.NavigateToScheduleAsync();
            _dispatcher.Dispatch(new ViewScheduleAction(null));
        });

        ViewScheduleCommand = new AsyncRelayCommand<ScheduleListItem>(async x =>
        {
            x.Schedule.IsEnabled = x.IsEnabled;
            await navigationService1.NavigateToScheduleAsync();
            _dispatcher.Dispatch(new ViewScheduleAction(x.Schedule));
        });

        _state.StateChanged += OnStateChanged;
        
        IsBusy = false;
    }

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
                await _databaseSeedService.SeedDefaultAlarmAsync();
                await _scheduleMigrationService.MigrateBibleGatewaySchedulesAsync();

                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

                var alarmSchedules = await scheduleDbContext.AlarmSchedules
                    .Include(x => x.BibleReadingSchedule)
                    .Include(x => x.Music)
                    .ToListAsync();

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

    private ObservableHashSet<ScheduleListItem> _schedules;

    public ObservableHashSet<ScheduleListItem> Schedules
    {
        get => _schedules;
        set => SetProperty(ref _schedules, value);
    }

    private void UpdateScheduleViewModels(ObservableHashSet<AlarmSchedule> schedules)
    {
        var newViewModels = new ObservableHashSet<ScheduleListItem>();
        var currentViewModelIds = new HashSet<int>();

        foreach (var schedule in schedules)
        {
            currentViewModelIds.Add(schedule.Id);
            
            if (_scheduleViewModels.TryGetValue(schedule.Id, out var existingViewModel))
            {
                existingViewModel.Initialize(schedule);
                newViewModels.Add(existingViewModel);
            }
            else
            {
                var viewModel = _scheduleListItemFactory(schedule);
                _scheduleViewModels[schedule.Id] = viewModel;
                newViewModels.Add(viewModel);
            }
        }

        var toRemove = _scheduleViewModels.Keys.Where(id => !currentViewModelIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (!_scheduleViewModels.TryGetValue(id, out var viewModel)) continue;
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


    private bool _loaded;

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

    private bool _initialized;
    private readonly SemaphoreSlim _lock = new(1);

    private void OnStateChanged(object sender, EventArgs e)
    {
        var stateValue = _state.Value;
        if (stateValue.Schedules == null) return;
        
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {

            UpdateScheduleViewModels(stateValue.Schedules);
            IsBusy = false;
        });
    }

    public void Dispose()
    {
        _state.StateChanged -= OnStateChanged;
        
        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                item.Dispose();
            }
        }

        _lock.Dispose();
    }
}