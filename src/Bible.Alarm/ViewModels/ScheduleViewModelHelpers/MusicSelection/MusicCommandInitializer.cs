#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
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
            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackCode,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open song publication modal (no more music type selection)
            var musicPublicationSelectionViewModel = serviceProvider.GetRequiredService<MusicPublicationSelectionViewModel>();
            await navigationService.OpenSongPublicationSelectionModalAsync(musicPublicationSelectionViewModel);

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
        // MusicType selection is removed - redirect to publication selection
        return CreateSelectSongPublicationCommand(getMusic, setMusic, scheduleId, isNewSchedule, musicUpdated);
    }

    public ICommand CreateSelectSongPublicationCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Check if publication is selectable (multiple options available)
            // If not selectable, don't open the modal
            var displayTextProvider = new MusicDisplayTextProvider(state, serviceProvider.GetRequiredService<IMediaService>(), serviceScopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(), logger: logger);
            var isSelectable = await displayTextProvider.GetIsSongPublicationSelectableAsync();
            if (!isSelectable)
            {
                return; // Only one option available, don't open modal
            }

            // Get music from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackCode,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open modal
            var songPublicationSelectionViewModel = serviceProvider.GetRequiredService<MusicPublicationSelectionViewModel>();
            await navigationService.OpenSongPublicationSelectionModalAsync(songPublicationSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicPublicationSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectMusicSectionCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            var displayTextProvider = new MusicDisplayTextProvider(state, serviceProvider.GetRequiredService<IMediaService>(), serviceScopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(), logger: logger);
            if (!await displayTextProvider.GetIsMusicSectionSelectableAsync())
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackCode,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create dedicated MusicSectionSelectionViewModel for music section selection
            var musicSectionSelectionViewModel = serviceProvider.GetRequiredService<Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModel>();
            await navigationService.OpenMusicSectionSelectionModalAsync(musicSectionSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicPublicationSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectTrackCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            var displayTextProvider = new MusicDisplayTextProvider(state, serviceProvider.GetRequiredService<IMediaService>(), serviceScopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(), logger: logger);
            if (!await displayTextProvider.GetIsMusicTrackSelectableAsync())
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackCode,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Create view model and open modal
            var musicTrackSelectionViewModel = serviceProvider.GetRequiredService<MusicTrackSelectionViewModel>();
            await navigationService.OpenMusicTrackSelectionModalAsync(musicTrackSelectionViewModel);

            // Map entity to DTO before dispatching
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicTrackSelectionAction(musicStateItem));
            }
        });
    }

    public ICommand CreateSelectMusicLanguageCommand(Func<AlarmMusic?> getMusic, Action<AlarmMusic?> setMusic, int scheduleId, bool isNewSchedule, bool musicUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            var displayTextProvider = new MusicDisplayTextProvider(state, serviceProvider.GetRequiredService<IMediaService>(), serviceScopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(), logger: logger);
            if (!await displayTextProvider.GetIsMusicLanguageSelectableAsync())
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            var loadedMusic = scheduleSelectionService.LoadMusicForSelection(
                scheduleId,
                isNewSchedule,
                getMusic(),
                currentSchedule?.MusicPublicationCode,
                currentSchedule?.MusicLanguageCode,
                currentSchedule?.MusicTrackCode,
                currentSchedule?.MusicRepeat);

            setMusic(loadedMusic);

            // Map entity to DTO before dispatching
            // MusicPublicationSelectionViewModel needs CurrentMusic in state to initialize
            if (loadedMusic != null)
            {
                var musicStateItem = mapper.Map<MusicStateItem>(loadedMusic);
                dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
            }

            // Create MusicPublicationSelectionViewModel instance to open the language modal
            var musicPublicationSelectionViewModel = serviceProvider.GetRequiredService<MusicPublicationSelectionViewModel>();

            // Open the language modal using the MusicPublicationSelectionViewModel
            await navigationService.OpenLanguageModalAsync(musicPublicationSelectionViewModel);
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
                toastService.ShowMessage("Repeat enabled");
            }
            else
            {
                toastService.Clear();
            }
        });
    }
}

