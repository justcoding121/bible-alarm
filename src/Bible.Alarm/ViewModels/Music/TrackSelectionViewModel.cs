#nullable enable

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class TrackSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;

    private readonly IMediaService mediaService;
    private readonly IToastService toastService;
    private readonly INavigationService navigationService;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IDownloadService downloadService;
    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;

    private AlarmMusic? current;
    private AlarmMusic? lastCurrent;
    private bool initComplete;

    // Track last music type, language code, and publication code to detect changes
    private MusicType? lastMusicType;
    private string? lastLanguageCode;
    private string? lastPublicationCode;

    private readonly Dictionary<MusicTrackListViewItemModel, PropertyChangedEventHandler> propertyChangedHandlers = [];

    public TrackSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.toastService = toastService;
        this.navigationService = navigationService;
        this.downloadService = downloadService;
        this.urlRefreshService = urlRefreshService;
        this.mapper = mapper;

        this.state = state;
        this.dispatcher = dispatcher;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        SetTrackCommand = new AsyncRelayCommand<MusicTrackListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            // Ensure current is set from state if it's null
            if (current == null)
            {
                var stateValue = state.Value;
                if (stateValue.CurrentMusic != null)
                {
                    current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
                }
            }

            // Always use CurrentSchedule as the source of truth for music type/language/publication codes
            // This ensures we use the latest state, not stale data from 'current' field
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                !currentSchedule.MusicType.HasValue ||
                string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
            {
                logger.Warning("TrackSelectionViewModel: SetTrackCommand - CurrentSchedule is null or missing required properties");
                return;
            }

            if (SelectedTrack != null)
            {
                SelectedTrack.IsSelected = false;
                SelectedTrack.Repeat = false;
            }

            SelectedTrack = x;
            SelectedTrack.IsSelected = true;

            // Update current for tracking purposes
            if (current == null)
            {
                current = new AlarmMusic();
            }
            current.TrackNumber = x.Number;
            current.Repeat = x.Repeat;

            // Map entity to DTO before dispatching
            // IMPORTANT: Include display names from list items (no database query needed)
            // Get music type/language/publication codes and display names from CurrentSchedule (they should already be populated)
            var trackSelectedItem = new MusicStateItem
            {
                MusicType = currentSchedule.MusicType.Value,
                LanguageCode = currentSchedule.MusicLanguageCode,
                PublicationCode = currentSchedule.MusicPublicationCode,
                TrackNumber = x.Number,
                Repeat = x.Repeat,
                // Store display names from list items and current state
                LanguageName = currentSchedule.MusicLanguageName,
                PublicationName = currentSchedule.MusicPublicationName,
                TrackName = x.Title
            };

            logger.Information("TrackSelectionViewModel: SetTrackCommand - Dispatching TrackSelectedAction. MusicType: {MusicType}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, TrackNumber: {TrackNumber}",
                trackSelectedItem.MusicType, trackSelectedItem.LanguageCode ?? "null", trackSelectedItem.PublicationCode, trackSelectedItem.TrackNumber);

            dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

            // Navigate back to schedule page
            await navigationService.PopModalAsync();
        });

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentMusic
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // For melodies: only require MusicType and PublicationCode (LanguageCode can be null)
        // For vocals: require MusicType, LanguageCode, and PublicationCode
        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // For vocals, language code is required
        if (newMusicType.Value == MusicType.Vocals && string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Check if music type, language code, or publication code changed (need to repopulate tracks)
        var musicTypeChanged = lastMusicType != newMusicType.Value;
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = musicTypeChanged || languageCodeChanged || publicationCodeChanged;

        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentMusic (for other properties like TrackNumber)
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal AlarmMusic from CurrentSchedule
            current = new AlarmMusic
            {
                MusicType = newMusicType.Value,
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        // If music type, language, or publication changed, repopulate tracks
        if (needsRepopulation && initComplete)
        {
            // For melodies, LanguageCode can be null, so only check PublicationCode
            // For vocals, LanguageCode is required (already checked above)
            if (newPublicationCode == null)
            {
                return;
            }
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                // Pass null/empty for languageCode if it's a melody (LanguageCode can be null for melodies)
                await Initialize(newLanguageCode ?? string.Empty, newPublicationCode);
                await Task.Delay(100); // Give CollectionView time to render
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });
        }
        else
        {
            // Update selected track when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedTrack);
        }
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentMusic
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // For melodies: only require MusicType and PublicationCode (LanguageCode can be null)
        // For vocals: require MusicType, LanguageCode, and PublicationCode
        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // For vocals, language code is required
        if (newMusicType.Value == MusicType.Vocals && string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentMusic (for other properties like TrackNumber)
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal AlarmMusic from CurrentSchedule
            current = new AlarmMusic
            {
                MusicType = newMusicType.Value,
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        initComplete = true;
        // For melodies, LanguageCode can be null, so only check PublicationCode
        // For vocals, LanguageCode is required (already checked above)
        if (newPublicationCode == null)
        {
            return;
        }
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            // Pass null/empty for languageCode if it's a melody (LanguageCode can be null for melodies)
            await Initialize(newLanguageCode ?? string.Empty, newPublicationCode);

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following book selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures tracks are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null || !stateValue.CurrentSchedule.MusicType.HasValue)
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // For melodies: only require MusicType and PublicationCode (LanguageCode can be null)
        // For vocals: require MusicType, LanguageCode, and PublicationCode
        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            return;
        }

        // For vocals, language code is required
        if (newMusicType.Value == MusicType.Vocals && string.IsNullOrEmpty(newLanguageCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current from CurrentSchedule
        current = new AlarmMusic
        {
            MusicType = newMusicType.Value,
            LanguageCode = newLanguageCode,
            PublicationCode = newPublicationCode,
            TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
        lastCurrent = current;

        // Ensure tracks are populated if not already initialized
        if (!initComplete || Tracks == null || Tracks.Count == 0)
        {
            // For melodies, LanguageCode can be null, so only check PublicationCode
            // For vocals, LanguageCode is required (already checked above)
            if (newPublicationCode == null)
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                return;
            }
            initComplete = true;
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            // Pass null for languageCode if it's a melody (LanguageCode can be null for melodies)
            await Initialize(newLanguageCode ?? string.Empty, newPublicationCode);
            await Task.Delay(100); // Give CollectionView time to render
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
        else
        {
            // Tracks are already loaded, just hide the busy indicator
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SetTrackCommand { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<MusicTrackListViewItemModel> tracks = [];

    public ObservableCollection<MusicTrackListViewItemModel> Tracks
    {
        get => tracks;
        set => SetProperty(ref tracks, value);
    }

    public MusicTrackListViewItemModel? SelectedTrack { get; set; }

    private readonly SemaphoreSlim @lock = new(1);

    private void SetSelectedTrack()
    {
        if (current == null || Tracks == null || Tracks.Count == 0)
        {
            return;
        }

        if (SelectedTrack != null)
        {
            SelectedTrack.IsSelected = false;
            SelectedTrack.Repeat = false;
        }

        var track = Tracks.FirstOrDefault(t => t.Number == current.TrackNumber);
        if (track != null)
        {
            SelectedTrack = track;
            SelectedTrack.IsSelected = true;
            SelectedTrack.Repeat = current.Repeat;
        }
    }

    private async Task Initialize(string languageCode, string publicationCode)
    {
        await PopulateTracks(languageCode, publicationCode);

        // Subscribe to PropertyChanged events for Repeat
        foreach (var track in Tracks)
        {
            SubscribeToTrackEvents(track);
        }

        // Subscribe to collection changes to handle new items
        Tracks.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (MusicTrackListViewItemModel item in e.NewItems)
                {
                    SubscribeToTrackEvents(item);
                }
            }

            if (e.OldItems == null)
            {
                return;
            }

            {
                foreach (MusicTrackListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromTrackEvents(item);
                }
            }
        };
    }

    private void SubscribeToTrackEvents(MusicTrackListViewItemModel track)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (sender is not MusicTrackListViewItemModel item)
            {
                return;
            }

            if (e.PropertyName == "Repeat")
            {
                HandleRepeatChanged(item);
            }
        };

        track.PropertyChanged += handler;
        propertyChangedHandlers[track] = handler;
    }

    private void UnsubscribeFromTrackEvents(MusicTrackListViewItemModel track)
    {
        if (!propertyChangedHandlers.TryGetValue(track, out var handler))
        {
            return;
        }

        track.PropertyChanged -= handler;
        propertyChangedHandlers.Remove(track);
    }


    private void HandleRepeatChanged(MusicTrackListViewItemModel track)
    {
        // Always use CurrentSchedule as the source of truth for music type/language/publication codes
        // This ensures we use the latest state, not stale data from 'current' field
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            !currentSchedule.MusicType.HasValue ||
            string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
        {
            logger.Warning("TrackSelectionViewModel: HandleRepeatChanged - CurrentSchedule is null or missing required properties");
            return;
        }

        // Update current for tracking purposes
        if (current == null)
        {
            current = new AlarmMusic();
        }
        current.Repeat = track.Repeat;
        current.TrackNumber = track.Number;

        // Map entity to DTO before dispatching
        // IMPORTANT: Include display names from list items (no database query needed)
        // Get music type/language/publication codes and display names from CurrentSchedule (they should already be populated)
        var trackSelectedItem = new MusicStateItem
        {
            MusicType = currentSchedule.MusicType.Value,
            LanguageCode = currentSchedule.MusicLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode,
            TrackNumber = track.Number,
            Repeat = track.Repeat,
            // Store display names from list items and current state
            LanguageName = currentSchedule.MusicLanguageName,
            PublicationName = currentSchedule.MusicPublicationName,
            TrackName = track.Title
        };

        logger.Information("TrackSelectionViewModel: HandleRepeatChanged - Dispatching TrackSelectedAction. MusicType: {MusicType}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, TrackNumber: {TrackNumber}, Repeat: {Repeat}",
            trackSelectedItem.MusicType, trackSelectedItem.LanguageCode ?? "null", trackSelectedItem.PublicationCode, trackSelectedItem.TrackNumber, trackSelectedItem.Repeat);

        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

        if (track.Repeat)
        {
            toastService.ShowMessage("Reminder will always repeat this track.");
        }
    }


    private async Task PopulateTracks(string languageCode, string publicationCode)
    {
        var isVocal = !string.IsNullOrEmpty(languageCode);

        // Run database operations off UI thread
        var tracks = await Task.Run(async () => isVocal
            ? await mediaService.GetVocalMusicTracks(languageCode, publicationCode)
            : await mediaService.GetMelodyMusicTracks(publicationCode));

        // Build the list of track view models
        var trackViewModelList = new List<MusicTrackListViewItemModel>();
        MusicTrackListViewItemModel? selectedTrack = null;

        foreach (var track in tracks.Select(x => x.Value))
        {
            var musicTrackListViewItemViewModel = new MusicTrackListViewItemModel(track, !isVocal);

            trackViewModelList.Add(musicTrackListViewItemViewModel);

            if (current == null)
            {
                continue;
            }

            if (current.TrackNumber != track.Number)
            {
                continue;
            }

            selectedTrack = musicTrackListViewItemViewModel;
            selectedTrack.IsSelected = true;
            selectedTrack.Repeat = current.Repeat;
        }

        // Assign the complete collection on main thread to ensure CollectionView refreshes
        // CollectionView responds better to property change notifications than collection modification
        // Follow the same pattern as language modal: assign collection but don't set IsBusy here
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Tracks = new ObservableCollection<MusicTrackListViewItemModel>(trackViewModelList);

            if (selectedTrack != null)
            {
                SelectedTrack = selectedTrack;
            }
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;

        if (Tracks != null)
        {
            foreach (var track in Tracks)
            {
                UnsubscribeFromTrackEvents(track);
            }
        }

        propertyChangedHandlers.Clear();

        @lock.Dispose();
    }
}

public sealed class MusicTrackListViewItemModel : ObservableObject, IComparable
{
    private readonly MusicTrack track;
    private readonly bool isMelody;

    public MusicTrackListViewItemModel(MusicTrack track, bool isMelody)
    {
        this.track = track;
        this.isMelody = isMelody;

        ToggleRepeatCommand = new RelayCommand(() => Repeat = !Repeat);
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string LookUpPath => track.Source?.LookUpPath ?? string.Empty;
    public int Number => track.Number;

    public string Title => isMelody ? $"Melody Number(s) {track.Title}" : track.Title;
    public string Url => track.Source?.Url ?? string.Empty;

    private bool repeat;

    public bool Repeat
    {
        get => repeat;
        set => SetProperty(ref repeat, value);
    }

    public ICommand ToggleRepeatCommand { get; set; }

    public int CompareTo(object? obj) => Number.CompareTo((obj as MusicTrackListViewItemModel)?.Number ?? 0);
}
