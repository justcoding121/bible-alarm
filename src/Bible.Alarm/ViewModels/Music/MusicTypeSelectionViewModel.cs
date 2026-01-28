#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Selectors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicTypeSelectionViewModel : ObservableObject, IDisposable
{
    private AlarmMusic? current;

    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    public MusicTypeSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;

        // Initialize from CurrentSchedule (source of truth) if available
        // CurrentSchedule is updated first and is authoritative
        var currentState = this.state.Value;
        if (currentState.CurrentSchedule != null && currentState.CurrentSchedule.MusicType.HasValue)
        {
            // Create a minimal AlarmMusic from CurrentSchedule for initialization
            current = new AlarmMusic
            {
                MusicType = currentState.CurrentSchedule.MusicType.Value,
                LanguageCode = currentState.CurrentSchedule.MusicLanguageCode ?? string.Empty,
                PublicationCode = currentState.CurrentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentState.CurrentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentState.CurrentSchedule.MusicRepeat ?? false
            };
        }
        // CurrentSchedule is the single source of truth - no fallback needed

        // Initialize selected music type immediately
        SetSelectedMusicType();

        this.state.StateChanged += OnStateOnStateChanged;

        MusicPublicationSelectionCommand = new AsyncRelayCommand<MusicTypeListItemViewModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            EnsureCurrentIsSet();
            var currentSchedule = this.state.Value.CurrentSchedule;
            var isSameMusicType = IsSameMusicType(currentSchedule, x.MusicType);

            if (x.MusicType == MusicType.VocalMusic)
            {
                await HandleVocalMusicSelectionAsync(currentSchedule, isSameMusicType);
            }
            else
            {
                await HandleMusicSelectionAsync(currentSchedule, isSameMusicType);
            }
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
    }

    private void OnStateOnStateChanged(object? o, EventArgs eventArgs)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentMusic
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null || !stateValue.CurrentSchedule.MusicType.HasValue)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;

        // Update current from CurrentSchedule
        current = new AlarmMusic
        {
            MusicType = currentSchedule.MusicType.Value,
            LanguageCode = currentSchedule.MusicLanguageCode ?? string.Empty,
            PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
            TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
            Repeat = currentSchedule.MusicRepeat ?? false
        };

        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SetSelectedMusicType();
            });

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following track/track selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    private void SetSelectedMusicType()
    {
        if (SelectedMusicType != null)
        {
            SelectedMusicType.IsSelected = false;
        }

        if (current == null)
        {
            return;
        }

        var musicType = MusicTypes.FirstOrDefault(y => y.MusicType == current.MusicType);
        if (musicType == null)
        {
            return;
        }

        SelectedMusicType = musicType;
        SelectedMusicType.IsSelected = true;
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures we always use CurrentSchedule as the source of truth.
    /// </summary>
    public void RefreshFromState()
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null || !stateValue.CurrentSchedule.MusicType.HasValue)
        {
            IsBusy = false;
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;

        // Update current from CurrentSchedule
        current = new AlarmMusic
        {
            MusicType = currentSchedule.MusicType.Value,
            LanguageCode = currentSchedule.MusicLanguageCode ?? string.Empty,
            PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
            TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
            Repeat = currentSchedule.MusicRepeat ?? false
        };

        // Update selected music type immediately
        SetSelectedMusicType();

        // MusicTypes is a static list (no DB loading), so set IsBusy = false immediately
        IsBusy = false;
    }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand MusicPublicationSelectionCommand { get; set; }

    public ObservableCollection<MusicTypeListItemViewModel> MusicTypes { get; set; }
        = new(
        [
            new MusicTypeListItemViewModel
            {
                MusicType = MusicType.Music,
                Name = "Instrumental Music"
            },
            new MusicTypeListItemViewModel
            {
                MusicType = MusicType.VocalMusic,
                Name = "Vocal Music"
            }
        ]);

    private MusicTypeListItemViewModel? selectedMusicType;

    public MusicTypeListItemViewModel? SelectedMusicType
    {
        get => selectedMusicType;
        set => SetProperty(ref selectedMusicType, value);
    }

    private void EnsureCurrentIsSet()
    {
        if (current == null)
        {
            // Derive from CurrentSchedule (single source of truth)
            var currentSchedule = this.state.Value.CurrentSchedule;
            if (currentSchedule != null && currentSchedule.MusicType.HasValue)
            {
                current = ApplicationSelectors.GetCurrentMusicEntity(this.state.Value, this.mapper);
            }
        }
    }

    private static bool IsSameMusicType(ScheduleStateItem? currentSchedule, MusicType musicType)
    {
        return currentSchedule != null && currentSchedule.MusicType == musicType;
    }

    private async Task HandleVocalMusicSelectionAsync(ScheduleStateItem? currentSchedule, bool isSameMusicType)
    {
        var result = await GetFirstLanguageAndSongPublicationAsync();
        if (result.LanguageCode == null)
        {
            return;
        }

        var tracks = await Task.Run(async () =>
            await mediaService.GetVocalMusicTracks(result.LanguageCode, result.PublicationCode));

        if (tracks == null || tracks.Count == 0)
        {
            return;
        }

        var (trackNumber, trackName) = GetTrackForVocalMusic(currentSchedule, isSameMusicType, result.LanguageCode, result.PublicationCode, tracks);
        var trackSelectedItem = CreateVocalMusicStateItem(currentSchedule, result.LanguageCode, result.PublicationCode, trackNumber, trackName, result.Language, result.FirstSongPublication);

        this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }

    private async Task<(string? LanguageCode, Language Language, string PublicationCode, VocalMusic FirstSongPublication)> GetFirstLanguageAndSongPublicationAsync()
    {
        var languages = await Task.Run(async () =>
            await mediaService.GetVocalMusicLanguages());

        if (languages == null || languages.Count == 0)
        {
            return (null, null!, string.Empty, null!);
        }

        // Default to English ("E") for Vocals, fallback to first available if English not present
        var languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key;

        if (string.IsNullOrEmpty(languageCode) || !languages.TryGetValue(languageCode, out var language))
        {
            return (null, null!, string.Empty, null!);
        }

        var songPublications = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(languageCode));

        if (songPublications == null || songPublications.Count == 0)
        {
            return (null, null!, string.Empty, null!);
        }

        var firstSongPublication = songPublications.FirstOrDefault();
        if (firstSongPublication.Value == null)
        {
            return (null, null!, string.Empty, null!);
        }

        return (languageCode, language, firstSongPublication.Key, firstSongPublication.Value);
    }

    private static (int TrackNumber, string TrackName) GetTrackForVocalMusic(
        ScheduleStateItem? currentSchedule,
        bool isSameMusicType,
        string languageCode,
        string publicationCode,
        SortedDictionary<int, MusicTrack> tracks)
    {
        if (isSameMusicType &&
            currentSchedule?.MusicLanguageCode == languageCode &&
            currentSchedule.MusicPublicationCode == publicationCode &&
            currentSchedule.MusicTrackNumber.HasValue &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            return (currentSchedule.MusicTrackNumber.Value, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number, randomTrack.Title);
    }

    private static MusicStateItem CreateVocalMusicStateItem(
        ScheduleStateItem? currentSchedule,
        string languageCode,
        string publicationCode,
        int trackNumber,
        string trackName,
        Language language,
        VocalMusic firstSongPublication)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.VocalMusic,
            LanguageCode = languageCode,
            PublicationCode = publicationCode,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = firstSongPublication.Name,
            TrackName = trackName
        };
    }

    private async Task HandleMusicSelectionAsync(ScheduleStateItem? currentSchedule, bool isSameMusicType)
    {
        // Get melody publications from database instead of hard-coding
        var melodyReleases = await Task.Run(async () =>
            await mediaService.GetMelodyMusicReleases());

        if (melodyReleases == null || melodyReleases.Count == 0)
        {
            return;
        }

        // Use current schedule's publication if available and valid, otherwise use first available
        string publicationCode;
        MelodyMusic melodyPublication;
        if (isSameMusicType &&
            currentSchedule?.MusicPublicationCode != null &&
            melodyReleases.TryGetValue(currentSchedule.MusicPublicationCode, out var existingMelody))
        {
            publicationCode = currentSchedule.MusicPublicationCode;
            melodyPublication = existingMelody;
        }
        else
        {
            var firstMelody = melodyReleases.FirstOrDefault();
            if (firstMelody.Value == null)
            {
                return;
            }
            publicationCode = firstMelody.Key;
            melodyPublication = firstMelody.Value;
        }

        var tracks = await Task.Run(async () =>
            await mediaService.GetMelodyMusicTracks(publicationCode));

        if (tracks == null || tracks.Count == 0)
        {
            return;
        }

        var (trackNumber, trackName) = GetTrackForMusic(currentSchedule, isSameMusicType, publicationCode, tracks);
        var trackSelectedItem = CreateMusicStateItem(currentSchedule, publicationCode, melodyPublication.Name, trackNumber, trackName);

        this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }

    private static (int TrackNumber, string TrackName) GetTrackForMusic(
        ScheduleStateItem? currentSchedule,
        bool isSameMusicType,
        string publicationCode,
        SortedDictionary<int, MusicTrack> tracks)
    {
        if (isSameMusicType &&
            currentSchedule?.MusicPublicationCode == publicationCode &&
            currentSchedule.MusicTrackNumber.HasValue &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            return (currentSchedule.MusicTrackNumber.Value, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number, randomTrack.Title);
    }

    private static MusicStateItem CreateMusicStateItem(
        ScheduleStateItem? currentSchedule,
        string publicationCode,
        string publicationName,
        int trackNumber,
        string trackName)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Music,
            PublicationCode = publicationCode,
            PublicationName = publicationName,
            TrackNumber = trackNumber,
            TrackName = trackName
        };
    }

    public void Dispose() => state.StateChanged -= OnStateOnStateChanged;
}

public sealed class MusicTypeListItemViewModel : ObservableObject, IComparable
{
    public MusicType MusicType { get; set; }
    public string Name { get; set; } = string.Empty;

    private bool isSelected;
    private bool isNavigating;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsNavigating
    {
        get => isNavigating;
        set => SetProperty(ref isNavigating, value);
    }

    public int CompareTo(object? obj) => obj is not MusicTypeListItemViewModel other ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);
}
