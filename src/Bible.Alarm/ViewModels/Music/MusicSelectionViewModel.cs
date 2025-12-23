using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
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
    private AlarmMusic current;

    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    public MusicSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
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
                LanguageCode = currentState.CurrentSchedule.MusicLanguageCode,
                PublicationCode = currentState.CurrentSchedule.MusicPublicationCode,
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
                // Ensure _current is set from state if it's null
                if (current == null)
                {
                    var currentItem = this.state.Value.CurrentMusic;
                    if (currentItem != null)
                    {
                        current = this.mapper.Map<AlarmMusic>(currentItem);
                    }
                }

                // Check if the selected music type is the same as the current music type
                var currentSchedule = this.state.Value.CurrentSchedule;
                var isSameMusicType = currentSchedule != null &&
                                     currentSchedule.MusicType == x.MusicType;

                if (x.MusicType == MusicType.Vocals)
                {
                    // For Vocals: Get the first language, first song book, and track, then navigate back
                    var languages = await Task.Run(async () =>
                        await mediaService.GetVocalMusicLanguages());

                    if (languages == null || languages.Count == 0)
                    {
                        return;
                    }

                    // Get the first language (prefer "E" if available, otherwise first in dictionary)
                    var languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
                    var language = languages[languageCode];

                    // Get the first song book for the selected language
                    var songBooks = await Task.Run(async () =>
                        await mediaService.GetVocalMusicReleases(languageCode));

                    if (songBooks == null || songBooks.Count == 0)
                    {
                        return;
                    }

                    // Get the first song book (first in dictionary)
                    var firstSongBook = songBooks.FirstOrDefault();
                    if (firstSongBook.Value == null)
                    {
                        return;
                    }

                    var publicationCode = firstSongBook.Key;

                    // Get tracks for the selected song book and language
                    var tracks = await Task.Run(async () =>
                        await mediaService.GetVocalMusicTracks(languageCode, publicationCode));

                    if (tracks == null || tracks.Count == 0)
                    {
                        return;
                    }

                    // If it's the same music type, language, and song book, preserve the current track (if valid)
                    // Otherwise, use a random track
                    int trackNumber;
                    string trackName;
                    
                    if (isSameMusicType &&
                        currentSchedule.MusicLanguageCode == languageCode &&
                        currentSchedule.MusicPublicationCode == publicationCode &&
                        currentSchedule.MusicTrackNumber.HasValue &&
                        tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
                    {
                        trackNumber = currentSchedule.MusicTrackNumber.Value;
                        trackName = currentTrack.Title;
                        System.Diagnostics.Debug.WriteLine($"MusicSelectionViewModel: SongBookSelectionCommand - Same music type, language, and song book selected (Vocals), preserving current track {trackNumber}");
                    }
                    else
                    {
                        // Different music type, language, or song book selected, use a random track
                        var tracksList = tracks.Values.ToList();
                        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                        trackNumber = randomTrack.Number;
                        trackName = randomTrack.Title;
                        System.Diagnostics.Debug.WriteLine($"MusicSelectionViewModel: SongBookSelectionCommand - Different music type/language/song book selected (Vocals), using random track {trackNumber}");
                    }

                    // Create MusicStateItem with selected music type, language, song book, and track
                    // IMPORTANT: Include display names from list items (no database query needed)
                    // Use CurrentSchedule for Repeat to ensure we use the latest state
                    var trackSelectedItem = new MusicStateItem
                    {
                        Repeat = currentSchedule?.MusicRepeat ?? false,
                        MusicType = MusicType.Vocals,
                        LanguageCode = languageCode,
                        PublicationCode = publicationCode,
                        TrackNumber = trackNumber,
                        // Store display names from list items
                        LanguageName = language.Name,
                        PublicationName = firstSongBook.Value.Name,
                        TrackName = trackName
                    };

                    // Dispatch TrackSelectedAction to update CurrentMusic
                    this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

                    // Navigate back to schedule page
                    await navigationService.PopModalAsync();
                }
                else
                {
                    // For Melodies: Get a track and update state, then navigate back
                    var tracks = await Task.Run(async () =>
                        await mediaService.GetMelodyMusicTracks("iam"));

                    if (tracks == null || tracks.Count == 0)
                    {
                        return;
                    }

                    // If it's the same music type and publication, preserve the current track (if valid)
                    // Otherwise, use a random track
                    int trackNumber;
                    string trackName;
                    
                    if (isSameMusicType &&
                        currentSchedule.MusicPublicationCode == "iam" &&
                        currentSchedule.MusicTrackNumber.HasValue &&
                        tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
                    {
                        trackNumber = currentSchedule.MusicTrackNumber.Value;
                        trackName = $"Melody Number(s) {currentTrack.Title}";
                        System.Diagnostics.Debug.WriteLine($"MusicSelectionViewModel: SongBookSelectionCommand - Same music type and publication selected (Melodies), preserving current track {trackNumber}");
                    }
                    else
                    {
                        // Different music type or publication selected, use a random track
                        var tracksList = tracks.Values.ToList();
                        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                        trackNumber = randomTrack.Number;
                        trackName = $"Melody Number(s) {randomTrack.Title}";
                        System.Diagnostics.Debug.WriteLine($"MusicSelectionViewModel: SongBookSelectionCommand - Different music type/publication selected (Melodies), using random track {trackNumber}");
                    }

                    // Create MusicStateItem with selected music type and track
                    // IMPORTANT: Include display names from list items (no database query needed)
                    // Use CurrentSchedule for Repeat to ensure we use the latest state
                    var trackSelectedItem = new MusicStateItem
                    {
                        Repeat = currentSchedule?.MusicRepeat ?? false,
                        MusicType = MusicType.Melodies,
                        PublicationCode = "iam",
                        TrackNumber = trackNumber,
                        // Store display names from list items (format melody track title with prefix)
                        TrackName = trackName
                    };

                    // Dispatch TrackSelectedAction to update CurrentMusic
                    this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

                    // Navigate back to schedule page
                    await navigationService.PopModalAsync();
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

    private void OnStateOnStateChanged(object o, EventArgs eventArgs)
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
            LanguageCode = currentSchedule.MusicLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode,
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
            LanguageCode = currentSchedule.MusicLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode,
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

    private MusicTypeListItemViewModel selectedMusicType;

    public MusicTypeListItemViewModel SelectedMusicType
    {
        get => selectedMusicType;
        set => SetProperty(ref selectedMusicType, value);
    }

    public void Dispose() => state.StateChanged -= OnStateOnStateChanged;
}

public sealed class MusicTypeListItemViewModel : ObservableObject, IComparable
{
    public MusicType MusicType { get; set; }
    public string Name { get; set; }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public int CompareTo(object obj) => obj is not MusicTypeListItemViewModel other ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);
}
