#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

/// <summary>
/// Handles initialization of commands for music selection.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicCommandInitializer
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IScheduleSelectionService scheduleSelectionService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;
    private readonly IToastService toastService;

    public MusicCommandInitializer(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        IServiceProvider serviceProvider,
        IToastService toastService)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.scheduleSelectionService = scheduleSelectionService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;
        this.toastService = toastService;
    }

    public ICommand CreateSelectMusicCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicType,
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackNumber,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open modal
            var musicSelectionViewModel = serviceProvider.GetRequiredService<MusicSelectionViewModel>();
            await navigationService.OpenMusicSelectionModalAsync(musicSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectMusicTypeCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicType,
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackNumber,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open modal
            var musicSelectionViewModel = serviceProvider.GetRequiredService<MusicSelectionViewModel>();
            await navigationService.OpenMusicSelectionModalAsync(musicSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectSongPublicationCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicType,
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackNumber,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open modal
            var songPublicationSelectionViewModel = serviceProvider.GetRequiredService<SongPublicationSelectionViewModel>();
            await navigationService.OpenSongPublicationSelectionModalAsync(songPublicationSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new SongPublicationSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectMusicSectionCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicType,
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackNumber,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create dedicated MusicSectionSelectionViewModel for music section selection
            var musicSectionSelectionViewModel = serviceProvider.GetRequiredService<Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModel>();
            await navigationService.OpenMusicSectionSelectionModalAsync(musicSectionSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new SongPublicationSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectTrackCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicType,
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackNumber,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open modal
            var trackSelectionViewModel = serviceProvider.GetRequiredService<TrackSelectionViewModel>();
            await navigationService.OpenMusicTrackSelectionModalAsync(trackSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new TrackSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectMusicLanguageCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicType,
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackNumber,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Map entity to DTO before dispatching
            // SongPublicationSelectionViewModel needs CurrentMusic in state to initialize
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
            }

            // Create SongPublicationSelectionViewModel instance to open the language modal
            var songPublicationSelectionViewModel = serviceProvider.GetRequiredService<SongPublicationSelectionViewModel>();

            // Open the language modal using the SongPublicationSelectionViewModel
            await navigationService.OpenLanguageModalAsync(songPublicationSelectionViewModel);
        });
    }

    public ICommand CreateToggleRepeatCommand()
    {
        return new RelayCommand(() =>
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning("ToggleRepeatCommand: CurrentSchedule is null, cannot toggle repeat");
                return;
            }

            // Toggle the repeat value
            var newRepeatValue = !(currentSchedule.MusicRepeat ?? false);

            logger.Debug("ToggleRepeatCommand: Toggling repeat from {OldValue} to {NewValue}",
                currentSchedule.MusicRepeat ?? false, newRepeatValue);

            // Update state - run DeepClone and mapping on background thread to avoid blocking UI
            _ = Task.Run(() =>
            {
                var clonedSchedule = currentSchedule.DeepClone();
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(clonedSchedule);
                scheduleStateItem.MusicRepeat = newRepeatValue;
                dispatcher.Dispatch(new Stores.Actions.Schedule.UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));
            });

            // Show toast message when repeat is enabled, hide it when disabled
            if (newRepeatValue)
            {
                toastService.ShowMessage("Repeat Enabled");
            }
            else
            {
                toastService.Clear();
            }
        });
    }
}

