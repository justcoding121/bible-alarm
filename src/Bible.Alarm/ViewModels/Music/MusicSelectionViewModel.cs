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

        // Initialize _current from state if available (map DTO to entity)
        if (this.state.Value.CurrentMusic != null)
        {
            current = this.mapper.Map<AlarmMusic>(this.state.Value.CurrentMusic);
        }

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

                if (x.MusicType == MusicType.Vocals)
                {
                    // For Vocals: Get the first language, first song book, and first track, then navigate back
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

                    // Get a random track (instead of first track for cascade changes)
                    var tracksList = tracks.Values.ToList();
                    var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                    var randomTrackNumber = randomTrack.Number;

                    // Create MusicStateItem with selected music type, language, song book, and random track
                    // IMPORTANT: Include display names from list items (no database query needed)
                    var trackSelectedItem = new MusicStateItem
                    {
                        Repeat = current?.Repeat ?? false,
                        MusicType = MusicType.Vocals,
                        LanguageCode = languageCode,
                        PublicationCode = publicationCode,
                        TrackNumber = randomTrackNumber,
                        // Store display names from list items
                        LanguageName = language.Name,
                        PublicationName = firstSongBook.Value.Name,
                        TrackName = randomTrack.Title
                    };

                    // Dispatch TrackSelectedAction to update CurrentMusic
                    this.dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

                    // Navigate back to schedule page
                    await navigationService.PopModalAsync();
                }
                else
                {
                    // For Melodies: Get a random track and update state, then navigate back
                    var tracks = await Task.Run(async () =>
                        await mediaService.GetMelodyMusicTracks("iam"));

                    if (tracks == null || tracks.Count == 0)
                    {
                        return;
                    }

                    // Get a random track (instead of first track for cascade changes)
                    var tracksList = tracks.Values.ToList();
                    var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                    var randomTrackNumber = randomTrack.Number;

                    // Create MusicStateItem with selected music type and random track
                    // IMPORTANT: Include display names from list items (no database query needed)
                    var trackSelectedItem = new MusicStateItem
                    {
                        Repeat = current?.Repeat ?? false,
                        MusicType = MusicType.Melodies,
                        PublicationCode = "iam",
                        TrackNumber = randomTrackNumber,
                        // Store display names from list items (format melody track title with prefix)
                        TrackName = $"Melody Number(s) {randomTrack.Title}"
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
        if (stateValue.CurrentMusic == null)
        {
            return;
        }
        // Map DTO to entity
        current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
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
