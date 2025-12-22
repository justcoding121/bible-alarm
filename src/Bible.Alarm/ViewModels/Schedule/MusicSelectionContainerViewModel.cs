using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class MusicSelectionContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IScheduleSelectionService scheduleSelectionService;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    private int scheduleId;
    private bool isNewSchedule;
    private bool musicUpdated;
    private AlarmMusic? music;
    private AlarmMusic? lastMusic;
    private AlarmSchedule? model;

    private string? cachedSongBookName;
    private string? lastMusicPublicationCode;
    private string? cachedTrackName;
    private int? lastMusicTrackNumber;
    private string? lastTrackPublicationCode;
    private string? lastTrackLanguageCode;
    private MusicType? lastTrackMusicType;
    private bool isUpdatingFromState;
    private bool? pendingMusicEnabled; // Optimistic update value

    public MusicSelectionContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.scheduleSelectionService = scheduleSelectionService;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        state.StateChanged += OnStateChanged;
        InitializeCommands();
        InitializeFromState();
    }

    private void InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            scheduleId = currentSchedule.Id;
            isNewSchedule = currentSchedule.Id <= 0;

            OnPropertyChanged(nameof(MusicEnabled));
            OnPropertyChanged(nameof(MusicTypeDisplayText));
            OnPropertyChanged(nameof(IsSongBookVisible));
            OnPropertyChanged(nameof(SongBookDisplayText));
            OnPropertyChanged(nameof(TrackDisplayText));

            // Load song book and track names asynchronously if music is enabled
            if (MusicEnabled)
            {
                Task.Run(async () =>
                {
                    var songBookName = await GetSongBookDisplayTextAsync();
                    var trackName = await GetTrackDisplayTextAsync();
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnPropertyChanged(nameof(SongBookDisplayText));
                        OnPropertyChanged(nameof(TrackDisplayText));
                    });
                });
            }
        }
    }

    private void InitializeCommands()
    {
        SelectMusicCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            music = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, music));

            await navigationService.NavigateToMusicSelectionAsync();

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(music);
            dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
        });

        SelectMusicTypeCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            music = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, music));

            await navigationService.NavigateToMusicSelectionAsync();

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(music);
            dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
        });

        SelectSongBookCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            music = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, music));

            await navigationService.NavigateToSongBookSelectionAsync();

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(music);
            dispatcher.Dispatch(new SongBookSelectionAction(musicStateItem));
        });

        SelectTrackCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            music = await Task.Run(async () =>
                await scheduleSelectionService.LoadMusicForSelectionAsync(scheduleId, isNewSchedule, musicUpdated, music));

            await navigationService.NavigateToTrackSelectionAsync();

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(music);
            dispatcher.Dispatch(new TrackSelectionAction(musicStateItem));
        });
    }

    public void SetModel(AlarmSchedule model)
    {
        this.model = model;
    }

    public void SetMusicUpdated(bool musicUpdated)
    {
        this.musicUpdated = musicUpdated;
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;
        
        // Initialize if schedule ID changed
        if (currentSchedule != null && currentSchedule.Id != scheduleId)
        {
            InitializeFromState();
        }
        
        // Check if MusicEnabled changed in state
        if (currentSchedule != null && model != null)
        {
            var stateMusicEnabled = currentSchedule.MusicEnabled;
            // Clear pending value since state has been updated
            if (pendingMusicEnabled.HasValue && pendingMusicEnabled.Value == stateMusicEnabled)
            {
                pendingMusicEnabled = null; // State now matches, clear pending
            }
            
            if (model.MusicEnabled != stateMusicEnabled)
            {
                // State has a different value, update model without dispatching
                isUpdatingFromState = true;
                try
                {
                    model.MusicEnabled = stateMusicEnabled;
                    pendingMusicEnabled = null; // Clear pending when updating from state
                    OnPropertyChanged(nameof(MusicEnabled));
                }
                finally
                {
                    isUpdatingFromState = false;
                }
            }
        }

        if (stateValue.CurrentMusic == null)
        {
            return;
        }

        // Check if the music actually changed by comparing properties
        var newMusicItem = stateValue.CurrentMusic;
        var hasChanged = lastMusic == null ||
                        music == null ||
                        lastMusic.LanguageCode != newMusicItem.LanguageCode ||
                        lastMusic.PublicationCode != newMusicItem.PublicationCode ||
                        lastMusic.MusicType != newMusicItem.MusicType ||
                        lastMusic.TrackNumber != newMusicItem.TrackNumber ||
                        (music != null &&
                         (music.TrackNumber != newMusicItem.TrackNumber ||
                          music.MusicType != newMusicItem.MusicType ||
                          music.LanguageCode != newMusicItem.LanguageCode ||
                          music.PublicationCode != newMusicItem.PublicationCode));

        if (!hasChanged)
        {
            return;
        }

        // Map DTO to entity
        var newMusic = mapper.Map<AlarmMusic>(newMusicItem);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            music = newMusic;
            lastMusic = newMusic;
            musicUpdated = true;
            OnPropertyChanged(nameof(MusicTypeDisplayText));
            OnPropertyChanged(nameof(IsSongBookVisible));
            OnPropertyChanged(nameof(SongBookDisplayText));
            OnPropertyChanged(nameof(TrackDisplayText));
            
            // Clear cached values when music changes
            cachedSongBookName = null;
            cachedTrackName = null;
            lastMusicPublicationCode = null;
            lastMusicTrackNumber = null;
            lastTrackPublicationCode = null;
            lastTrackLanguageCode = null;
            lastTrackMusicType = null;
            
            // Load song book and track names asynchronously
            Task.Run(async () =>
            {
                var songBookName = await GetSongBookDisplayTextAsync();
                var trackName = await GetTrackDisplayTextAsync();
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(SongBookDisplayText));
                    OnPropertyChanged(nameof(TrackDisplayText));
                });
            });
        });
    }

    public ICommand SelectMusicCommand { get; private set; }
    public ICommand SelectMusicTypeCommand { get; private set; }
    public ICommand SelectSongBookCommand { get; private set; }
    public ICommand SelectTrackCommand { get; private set; }

    public bool MusicEnabled
    {
        get
        {
            // Return pending value if set (optimistic update), otherwise read from state
            if (pendingMusicEnabled.HasValue)
            {
                return pendingMusicEnabled.Value;
            }
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.MusicEnabled ?? false;
        }
        set
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning("MusicEnabled setter: CurrentSchedule is null, cannot update");
                return;
            }

            // Check if the value is actually different from the current state
            var currentValue = MusicEnabled;
            if (currentValue == value)
            {
                // Value hasn't changed, don't dispatch
                logger.Debug("MusicEnabled setter: Value unchanged ({Value}), skipping", value);
                return;
            }

            // Prevent dispatching if this update is coming from state (not user interaction)
            if (isUpdatingFromState)
            {
                logger.Debug("MusicEnabled setter: Update from state, skipping dispatch");
                if (model != null)
                {
                    model.MusicEnabled = value;
                }
                pendingMusicEnabled = null; // Clear pending when updating from state
                OnPropertyChanged();
                return;
            }

            logger.Debug("MusicEnabled setter: Setting to {Value} (was {CurrentValue})", value, currentValue);

            // Set optimistic update value immediately
            pendingMusicEnabled = value;
            if (model != null)
            {
                model.MusicEnabled = value;
            }
            
            // Trigger PropertyChanged immediately so animation starts
            OnPropertyChanged(nameof(MusicEnabled));

            // Update state (will clear pendingMusicEnabled when state updates)
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(currentSchedule.DeepClone());
            scheduleStateItem.MusicEnabled = value;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false));
        }
    }

    public string MusicTypeDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null || !currentSchedule.MusicType.HasValue)
            {
                return string.Empty;
            }

            return currentSchedule.MusicType.Value switch
            {
                MusicType.Melodies => "Orchestral Melodies",
                MusicType.Vocals => "Vocals",
                _ => string.Empty
            };
        }
    }

    public bool IsSongBookVisible
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule != null && 
                   currentSchedule.MusicType.HasValue && 
                   currentSchedule.MusicType.Value == MusicType.Vocals;
        }
    }

    public string SongBookDisplayText
    {
        get
        {
            if (!IsSongBookVisible)
            {
                return string.Empty;
            }

            // Return cached value if available
            if (!string.IsNullOrEmpty(cachedSongBookName))
            {
                return cachedSongBookName;
            }

            // Return empty if not loaded yet (will be loaded asynchronously)
            return string.Empty;
        }
    }

    public async Task<string> GetSongBookDisplayTextAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || 
            !currentSchedule.MusicType.HasValue || 
            currentSchedule.MusicType.Value != MusicType.Vocals ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode) ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageCode))
        {
            return string.Empty;
        }

        // Return cached value if publication code hasn't changed
        if (!string.IsNullOrEmpty(cachedSongBookName) && 
            lastMusicPublicationCode == currentSchedule.MusicPublicationCode)
        {
            return cachedSongBookName;
        }

        try
        {
            var releases = await mediaService.GetVocalMusicReleases(currentSchedule.MusicLanguageCode);
            if (releases.TryGetValue(currentSchedule.MusicPublicationCode, out var release))
            {
                cachedSongBookName = release.Name;
                lastMusicPublicationCode = currentSchedule.MusicPublicationCode;
                return cachedSongBookName;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting song book name");
        }

        return string.Empty;
    }

    public string TrackDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null || 
                !currentSchedule.MusicType.HasValue ||
                !currentSchedule.MusicTrackNumber.HasValue ||
                currentSchedule.MusicTrackNumber.Value <= 0)
            {
                return string.Empty;
            }

            // Return cached value if available
            if (!string.IsNullOrEmpty(cachedTrackName))
            {
                return cachedTrackName;
            }

            // Return empty if not loaded yet (will be loaded asynchronously)
            return string.Empty;
        }
    }

    public async Task<string> GetTrackDisplayTextAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || 
            !currentSchedule.MusicType.HasValue ||
            !currentSchedule.MusicTrackNumber.HasValue ||
            currentSchedule.MusicTrackNumber.Value <= 0)
        {
            return string.Empty;
        }

        // Return cached value if nothing changed
        if (!string.IsNullOrEmpty(cachedTrackName) &&
            lastMusicTrackNumber == currentSchedule.MusicTrackNumber.Value &&
            lastTrackPublicationCode == currentSchedule.MusicPublicationCode &&
            lastTrackLanguageCode == currentSchedule.MusicLanguageCode &&
            lastTrackMusicType == currentSchedule.MusicType.Value)
        {
            return cachedTrackName;
        }

        try
        {
            string? trackName = null;
            if (currentSchedule.MusicType.Value == MusicType.Melodies)
            {
                var tracks = await mediaService.GetMelodyMusicTracks(
                    currentSchedule.MusicPublicationCode ?? "iam");
                if (tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var track))
                {
                    trackName = track.Title;
                }
            }
            else if (currentSchedule.MusicType.Value == MusicType.Vocals)
            {
                var tracks = await mediaService.GetVocalMusicTracks(
                    currentSchedule.MusicLanguageCode,
                    currentSchedule.MusicPublicationCode);
                if (tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var track))
                {
                    trackName = track.Title;
                }
            }

            if (!string.IsNullOrEmpty(trackName))
            {
                cachedTrackName = trackName;
                lastMusicTrackNumber = currentSchedule.MusicTrackNumber.Value;
                lastTrackPublicationCode = currentSchedule.MusicPublicationCode;
                lastTrackLanguageCode = currentSchedule.MusicLanguageCode;
                lastTrackMusicType = currentSchedule.MusicType.Value;
                return cachedTrackName;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting track name");
        }

        return string.Empty;
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

