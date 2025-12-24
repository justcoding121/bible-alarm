#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicSelectionViewModel : ObservableObject, IDisposable
{
    private AlarmMusic? current;

    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    public MusicSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
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
        else if (this.state.Value.CurrentMusic != null)
        {
            // Fallback to CurrentMusic if CurrentSchedule doesn't have music info
            current = this.mapper.Map<AlarmMusic>(this.state.Value.CurrentMusic);
        }

        // Initialize selected music type immediately
        SetSelectedMusicType();

        this.state.StateChanged += OnStateOnStateChanged;

        SongBookSelectionCommand = new AsyncRelayCommand<MusicTypeListItemViewModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            try
            {
                EnsureCurrentIsSet();
                var currentSchedule = this.state.Value.CurrentSchedule;
                var isSameMusicType = IsSameMusicType(currentSchedule, x.MusicType);

                if (x.MusicType == MusicType.Vocals)
                {
                    await HandleVocalsSelectionAsync(currentSchedule, isSameMusicType);
                }
                else
                {
                    await HandleMelodiesSelectionAsync(currentSchedule, isSameMusicType);
                }
            }
            finally
            {
                IsBusy = false;
            }
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopModalAsync();
            IsBusy = false;
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
            // Add a small delay to prevent blank page flash (following chapter/track selection pattern)
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
    public ICommand SongBookSelectionCommand { get; set; }

    public ObservableCollection<MusicTypeListItemViewModel> MusicTypes { get; set; }
        = new(
        [
            new MusicTypeListItemViewModel
            {
                MusicType = MusicType.Melodies,
                Name = "Orchestral Melodies"
            },
            new MusicTypeListItemViewModel
            {
                MusicType = MusicType.Vocals,
                Name = "Vocals"
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
            var currentItem = this.state.Value.CurrentMusic;
            if (currentItem != null)
            {
                current = this.mapper.Map<AlarmMusic>(currentItem);
            }
        }
    }

    private static bool IsSameMusicType(ScheduleStateItem? currentSchedule, MusicType musicType)
    {
        return currentSchedule != null && currentSchedule.MusicType == musicType;
    }

    private async Task HandleVocalsSelectionAsync(ScheduleStateItem? currentSchedule, bool isSameMusicType)
    {
        var result = await GetFirstLanguageAndSongBookAsync();
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

        var (trackNumber, trackName) = GetTrackForVocals(currentSchedule, isSameMusicType, result.LanguageCode, result.PublicationCode, tracks);
        var trackSelectedItem = CreateVocalsMusicStateItem(currentSchedule, result.LanguageCode, result.PublicationCode, trackNumber, trackName, result.Language, result.FirstSongBook);
        
        this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }

    private async Task<(string? LanguageCode, Language Language, string PublicationCode, VocalMusic FirstSongBook)> GetFirstLanguageAndSongBookAsync()
    {
        var languages = await Task.Run(async () =>
            await mediaService.GetVocalMusicLanguages());

        if (languages == null || languages.Count == 0)
        {
            return (null, null!, string.Empty, null!);
        }

        var languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
        var language = languages[languageCode];

        var songBooks = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(languageCode));

        if (songBooks == null || songBooks.Count == 0)
        {
            return (null, null!, string.Empty, null!);
        }

        var firstSongBook = songBooks.FirstOrDefault();
        if (firstSongBook.Value == null)
        {
            return (null, null!, string.Empty, null!);
        }

        return (languageCode, language, firstSongBook.Key, firstSongBook.Value);
    }

    private static (int TrackNumber, string TrackName) GetTrackForVocals(
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

    private static MusicStateItem CreateVocalsMusicStateItem(
        ScheduleStateItem? currentSchedule,
        string languageCode,
        string publicationCode,
        int trackNumber,
        string trackName,
        Language language,
        VocalMusic firstSongBook)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Vocals,
            LanguageCode = languageCode,
            PublicationCode = publicationCode,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            PublicationName = firstSongBook.Name,
            TrackName = trackName
        };
    }

    private async Task HandleMelodiesSelectionAsync(ScheduleStateItem? currentSchedule, bool isSameMusicType)
    {
        var tracks = await Task.Run(async () =>
            await mediaService.GetMelodyMusicTracks("iam"));

        if (tracks == null || tracks.Count == 0)
        {
            return;
        }

        var (trackNumber, trackName) = GetTrackForMelodies(currentSchedule, isSameMusicType, tracks);
        var trackSelectedItem = CreateMelodiesMusicStateItem(currentSchedule, trackNumber, trackName);
        
        this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }

    private static (int TrackNumber, string TrackName) GetTrackForMelodies(
        ScheduleStateItem? currentSchedule,
        bool isSameMusicType,
        SortedDictionary<int, MusicTrack> tracks)
    {
        if (isSameMusicType &&
            currentSchedule?.MusicPublicationCode == "iam" &&
            currentSchedule.MusicTrackNumber.HasValue &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            return (currentSchedule.MusicTrackNumber.Value, $"Melody Number(s) {currentTrack.Title}");
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number, $"Melody Number(s) {randomTrack.Title}");
    }

    private static MusicStateItem CreateMelodiesMusicStateItem(ScheduleStateItem? currentSchedule, int trackNumber, string trackName)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Melodies,
            PublicationCode = "iam",
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

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public int CompareTo(object? obj) => obj is not MusicTypeListItemViewModel other ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);
}
