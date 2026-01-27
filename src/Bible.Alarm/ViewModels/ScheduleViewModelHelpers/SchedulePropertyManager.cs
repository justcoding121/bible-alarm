#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles property management for ScheduleViewModel.
/// </summary>
public sealed class SchedulePropertyManager : ObservableObject
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;

    public SchedulePropertyManager(IState<ApplicationState> state, ILogger logger)
    {
        this.logger = logger;
        this.state = state;
    }

    // Container ViewModels
    private BiblePublicationSelectionContainerViewModel? bibleSelectionContainerViewModel;
    private MusicSelectionContainerViewModel? musicSelectionContainerViewModel;
    private NumberOfTrackContainerViewModel? numberOfTrackContainerViewModel;
    private ScheduleDetailsContainerViewModel? scheduleDetailsContainerViewModel;
    private AlarmSettingsContainerViewModel? alarmSettingsContainerViewModel;

    public BiblePublicationSelectionContainerViewModel? BibleSelectionContainerViewModel
    {
        get => bibleSelectionContainerViewModel;
        set => SetProperty(ref bibleSelectionContainerViewModel, value);
    }

    public MusicSelectionContainerViewModel? MusicSelectionContainerViewModel
    {
        get => musicSelectionContainerViewModel;
        set => SetProperty(ref musicSelectionContainerViewModel, value);
    }

    public NumberOfTrackContainerViewModel? NumberOfTrackContainerViewModel
    {
        get => numberOfTrackContainerViewModel;
        set => SetProperty(ref numberOfTrackContainerViewModel, value);
    }

    public ScheduleDetailsContainerViewModel? ScheduleDetailsContainerViewModel
    {
        get => scheduleDetailsContainerViewModel;
        set => SetProperty(ref scheduleDetailsContainerViewModel, value);
    }

    public AlarmSettingsContainerViewModel? AlarmSettingsContainerViewModel
    {
        get => alarmSettingsContainerViewModel;
        set => SetProperty(ref alarmSettingsContainerViewModel, value);
    }

    // Properties
    private bool isBusy;
    private bool isNewSchedule;
    private bool isExistingSchedule;
    private bool isScrolledToBottom;
    private bool isSchedulePageOverlayVisible = true;
    private bool isCancelBusy;
    private bool isSaveBusy;
    private bool isDeleteBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public bool IsNewSchedule
    {
        get => isNewSchedule;
        set
        {
            IsExistingSchedule = !value;
            SetProperty(ref isNewSchedule, value);
        }
    }

    public bool IsScrolledToBottom
    {
        get => isScrolledToBottom;
        set => SetProperty(ref isScrolledToBottom, value);
    }

    public bool IsExistingSchedule
    {
        get => isExistingSchedule;
        private set => SetProperty(ref isExistingSchedule, value);
    }

    public bool IsSchedulePageOverlayVisible
    {
        get => isSchedulePageOverlayVisible;
        set
        {
            if (SetProperty(ref isSchedulePageOverlayVisible, value))
            {
                logger.Debug("IsSchedulePageOverlayVisible: Property changed to {Value}", value);
            }
        }
    }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    public bool IsSaveBusy
    {
        get => isSaveBusy;
        set => SetProperty(ref isSaveBusy, value);
    }

    public bool IsDeleteBusy
    {
        get => isDeleteBusy;
        set => SetProperty(ref isDeleteBusy, value);
    }

    // Computed properties from state
    public string Name => SchedulePropertyHelper.GetName(state.Value.CurrentSchedule);

    public bool IsEnabled => SchedulePropertyHelper.GetIsEnabled(state.Value.CurrentSchedule);

    public DaysOfWeek DaysOfWeek => SchedulePropertyHelper.GetDaysOfWeek(state.Value.CurrentSchedule);

    public TimeSpan Time => SchedulePropertyHelper.GetTime(state.Value.CurrentSchedule);

    public bool MusicEnabled => SchedulePropertyHelper.GetMusicEnabled(state.Value.CurrentSchedule);

    public int ScheduleId => SchedulePropertyHelper.GetScheduleId(state.Value.CurrentSchedule);

    // Methods for property change notifications
    public void NotifySchedulePropertiesChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(DaysOfWeek));
            OnPropertyChanged(nameof(Time));
            OnPropertyChanged(nameof(MusicEnabled));
        });
    }

    public void SetIsBusy(bool value) => IsBusy = value;
    public void SetIsNewSchedule(bool value) => IsNewSchedule = value;
    public void SetIsSchedulePageOverlayVisible(bool value) => IsSchedulePageOverlayVisible = value;
}
