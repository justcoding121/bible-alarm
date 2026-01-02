#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
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
            // Run database operations off UI thread
            var loadedMusic = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, getMusic()));
            setMusic(loadedMusic);

            // Create view model and open modal
            var musicSelectionViewModel = serviceProvider.GetRequiredService<MusicSelectionViewModel>();
            await navigationService.OpenMusicSelectionModalAsync(musicSelectionViewModel);

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
            dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
        });
    }

    public ICommand CreateSelectMusicTypeCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedMusic = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, getMusic()));
            setMusic(loadedMusic);

            // Create view model and open modal
            var musicSelectionViewModel = serviceProvider.GetRequiredService<MusicSelectionViewModel>();
            await navigationService.OpenMusicSelectionModalAsync(musicSelectionViewModel);

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
            dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
        });
    }

    public ICommand CreateSelectSongBookCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedMusic = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, getMusic()));
            setMusic(loadedMusic);

            // Create view model and open modal
            var songBookSelectionViewModel = serviceProvider.GetRequiredService<SongBookSelectionViewModel>();
            await navigationService.OpenSongBookSelectionModalAsync(songBookSelectionViewModel);

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
            dispatcher.Dispatch(new SongBookSelectionAction(musicStateItem));
        });
    }

    public ICommand CreateSelectTrackCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedMusic = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, getMusic()));
            setMusic(loadedMusic);

            // Create view model and open modal
            var trackSelectionViewModel = serviceProvider.GetRequiredService<TrackSelectionViewModel>();
            await navigationService.OpenTrackSelectionModalAsync(trackSelectionViewModel);

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
            dispatcher.Dispatch(new TrackSelectionAction(musicStateItem));
        });
    }

    public ICommand CreateSelectMusicLanguageCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedMusic = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, getMusic()));
            setMusic(loadedMusic);

            // Create SongBookSelectionViewModel instance to open the language modal
            var songBookSelectionViewModel = serviceProvider.GetRequiredService<SongBookSelectionViewModel>();

            // Open the language modal using the SongBookSelectionViewModel
            await navigationService.OpenLanguageModalAsync(songBookSelectionViewModel);
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

            // Update state
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(currentSchedule.DeepClone());
            scheduleStateItem.MusicRepeat = newRepeatValue;
            dispatcher.Dispatch(new Stores.Actions.Schedule.UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));

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

