#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class SongBookSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    private AlarmMusic? current;
    private bool initComplete;
    private AlarmMusic? lastCurrent;
    private PropertyChangedEventHandler? propertyChangedHandler;

    // Track last music type and language code to detect changes
    private MusicType? lastMusicType;
    private string? lastLanguageCode;

    public SongBookSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;

        this.state.StateChanged += OnMusicInitialized;
        this.state.StateChanged += OnMusicChanged;

        // Check current state immediately in case state is already set
        var currentState = this.state.Value;
        if (currentState.CurrentMusic != null)
        {
            OnMusicInitialized(null, EventArgs.Empty);
        }

        TrackSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            try
            {
                EnsureCurrentIsSet();
                var languageCode = CurrentLanguage?.Code ?? string.Empty;
                if (string.IsNullOrEmpty(languageCode))
                {
                    return;
                }

                var (trackNumber, trackName) = await GetTrackForSongBookAsync(x, languageCode);
                if (trackNumber == 0)
                {
                    return;
                }

                var trackSelectedItem = CreateMusicStateItemForSongBook(x, languageCode, trackNumber, trackName);
                dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
                await navigationService.PopModalAsync();
            }
            finally
            {
                IsBusy = false;
            }
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            try
            {
                // Ensure current is set from state if it's null (especially on first load)
                if (current == null)
                {
                    var currentItem = state.Value.CurrentMusic;
                    if (currentItem != null)
                    {
                        current = mapper.Map<AlarmMusic>(currentItem);
                    }
                    else
                    {
                        // If CurrentMusic is null, create a minimal AlarmMusic from CurrentSchedule
                        var currentSchedule = state.Value.CurrentSchedule;
                        if (currentSchedule != null && currentSchedule.MusicType.HasValue)
                        {
                            current = new AlarmMusic
                            {
                                MusicType = currentSchedule.MusicType.Value,
                                LanguageCode = currentSchedule.MusicLanguageCode ?? string.Empty,
                                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                                Repeat = currentSchedule.MusicRepeat ?? false
                            };
                        }
                    }
                }

                // Ensure languages are populated before opening the modal
                // Always repopulate to ensure data is fresh, especially on first load
                await PopulateLanguages();

                // Wait a moment to ensure the collection is assigned and UI is ready
                await Task.Delay(50);

                // Double-check that languages are populated before opening modal
                if (Languages == null || Languages.Count == 0)
                {
                    // If still empty, wait a bit more and try once more
                    await Task.Delay(100);
                    await PopulateLanguages();
                    await Task.Delay(50); // Additional delay after second attempt
                }

                await navigationService.OpenLanguageModalAsync(this);
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

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            try
            {
                EnsureCurrentIsSet();
                UpdateSelectedLanguage(x);

                var (publicationCode, trackNumber, trackName, publicationName) = await GetFirstSongBookAndTrackForLanguageAsync(x);
                if (publicationCode == null)
                {
                    return;
                }

                var trackSelectedItem = CreateMusicStateItemForLanguage(x, publicationCode, trackNumber, trackName, publicationName);
                dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
                await navigationService.PopModalAsync();
            }
            finally
            {
                IsBusy = false;
            }
        });
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

        if (!newMusicType.HasValue || string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Check if music type or language code changed (need to repopulate song books)
        var musicTypeChanged = lastMusicType != newMusicType.Value;
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var needsRepopulation = musicTypeChanged || languageCodeChanged;

        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;

        // Update current if we have CurrentMusic (for other properties like PublicationCode)
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
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        // If music type or language changed, repopulate song books
        if (needsRepopulation && initComplete && newMusicType.Value == MusicType.Vocals)
        {
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                await PopulateSongBooks(newLanguageCode);
                await Task.Delay(100); // Give CollectionView time to render
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });
        }
        else
        {
            // Update selected song book when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedSongBook);
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

        if (!newMusicType.HasValue || string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;

        // Update current if we have CurrentMusic (for other properties like PublicationCode)
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
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize();

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following chapter/track selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    private void SetSelectedSongBook()
    {
        if (current == null)
        {
            return;
        }

        if (SelectedSongBook != null)
        {
            SelectedSongBook!.IsSelected = false;
        }

        if (!songBookVMsMapping.TryGetValue(current.PublicationCode, out var songBook))
        {
            return;
        }

        SelectedSongBook = songBook;
        SelectedSongBook!.IsSelected = true;
    }

    public ICommand BackCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<PublicationListViewItemModel>? songBooks;

    public ObservableCollection<PublicationListViewItemModel> SongBooks
    {
        get => songBooks ??= [];
        set => SetProperty(ref songBooks, value);
    }

    private ObservableCollection<LanguageListViewItemModel>? languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    private LanguageListViewItemModel? currentLanguage;

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    private string languageSearchTerm = string.Empty;

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public PublicationListViewItemModel? SelectedSongBook { get; set; }

    public object? SelectedItem => CurrentLanguage;

    private async Task Initialize()
    {
        if (current == null)
        {
            return;
        }

        var languageCode = current.LanguageCode;

        if (languageCode == null)
        {
            var languages = await mediaService.GetVocalMusicLanguages();
            languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
        }

        current.LanguageCode = languageCode;

        await PopulateLanguages();
        await PopulateSongBooks(languageCode);

        propertyChangedHandler = (sender, e) =>
        {
            if (e.PropertyName == "LanguageSearchTerm")
            {
                _ = PopulateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += propertyChangedHandler;
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        // Run database operations off UI thread
        var languages = await Task.Run(async () =>
            await mediaService.GetVocalMusicLanguages());
        var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

        // Trim the search term before using it
        var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

        foreach (var language in languages.Select(x => x.Value)
                     .Where(x => trimmedSearchTerm == null
                                 || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Name))
        {
            var languageVm = new LanguageListViewItemModel(language);

            languageVMs.Add(languageVm);

            if (current == null || languageVm.Code != current.LanguageCode)
            {
                continue;
            }

            languageVm.IsSelected = true;
            CurrentLanguage = languageVm;
        }

        // Assign collection on main thread to ensure UI updates
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Languages = languageVMs;
        });
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures languages are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
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

        // Ensure languages are populated
        if (Languages == null || Languages.Count == 0)
        {
            await PopulateLanguages();
        }
    }

    private readonly Dictionary<string, PublicationListViewItemModel> songBookVMsMapping = [];

    private async Task PopulateSongBooks(string languageCode)
    {
        songBookVMsMapping.Clear();

        // Run database operations off UI thread
        var songBooks = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(languageCode));
        var songBookVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var release in songBooks.Select(x => x.Value))
        {
            var songBookListViewItemModel = new PublicationListViewItemModel(release);

            songBookVMs.Add(songBookListViewItemModel);
            songBookVMsMapping.Add(songBookListViewItemModel.Code, songBookListViewItemModel);

            if (current == null
                || current.MusicType != MusicType.Vocals
                || current.LanguageCode != languageCode
                || current.PublicationCode != release.Code)
            {
                continue;
            }

            songBookListViewItemModel.IsSelected = true;
            SelectedSongBook = songBookListViewItemModel;
        }

        // Assign collection on main thread to ensure UI updates before IsBusy is set to false
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            SongBooks = songBookVMs;
        });
    }

    private void EnsureCurrentIsSet()
    {
        if (current == null)
        {
            var currentItem = state.Value.CurrentMusic;
            if (currentItem != null)
            {
                current = mapper.Map<AlarmMusic>(currentItem);
            }
        }
    }

    private async Task<(int TrackNumber, string TrackName)> GetTrackForSongBookAsync(PublicationListViewItemModel songBook, string languageCode)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameSongBook = IsSameSongBook(currentSchedule, languageCode, songBook.Code);

        var tracks = await Task.Run(async () =>
            await mediaService.GetVocalMusicTracks(languageCode, songBook.Code));

        if (tracks == null || tracks.Count == 0)
        {
            return (0, string.Empty);
        }

        if (isSameSongBook &&
            currentSchedule?.MusicTrackNumber.HasValue == true &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            return (currentSchedule.MusicTrackNumber.Value, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number, randomTrack.Title);
    }

    private static bool IsSameSongBook(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.Vocals &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }

    private MusicStateItem CreateMusicStateItemForSongBook(PublicationListViewItemModel songBook, string languageCode, int trackNumber, string trackName)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Vocals,
            LanguageCode = languageCode,
            PublicationCode = songBook.Code,
            TrackNumber = trackNumber,
            LanguageName = CurrentLanguage?.Name,
            PublicationName = songBook.Name,
            TrackName = trackName
        };
    }

    private void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        if (CurrentLanguage != null)
        {
            CurrentLanguage.IsSelected = false;
        }

        CurrentLanguage = language;
        CurrentLanguage!.IsSelected = true;
    }

    private async Task<(string? PublicationCode, int TrackNumber, string TrackName, string PublicationName)> GetFirstSongBookAndTrackForLanguageAsync(LanguageListViewItemModel language)
    {
        var songBooks = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(language.Code));

        if (songBooks == null || songBooks.Count == 0)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var firstSongBook = songBooks.FirstOrDefault();
        if (firstSongBook.Value == null)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var publicationCode = firstSongBook.Key;
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameLanguage = IsSameLanguageAndSongBook(currentSchedule, language.Code, publicationCode);

        var tracks = await Task.Run(async () =>
            await mediaService.GetVocalMusicTracks(language.Code, publicationCode));

        if (tracks == null || tracks.Count == 0)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        int trackNumber;
        string trackName;

        if (isSameLanguage &&
            currentSchedule?.MusicTrackNumber.HasValue == true &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            trackNumber = currentSchedule.MusicTrackNumber.Value;
            trackName = currentTrack.Title;
        }
        else
        {
            var tracksList = tracks.Values.ToList();
            var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
            trackNumber = randomTrack.Number;
            trackName = randomTrack.Title;
        }

        return (publicationCode, trackNumber, trackName, firstSongBook.Value.Name);
    }

    private static bool IsSameLanguageAndSongBook(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.Vocals &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }

    private MusicStateItem CreateMusicStateItemForLanguage(LanguageListViewItemModel language, string publicationCode, int trackNumber, string trackName, string publicationName)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Vocals,
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            PublicationName = publicationName,
            TrackName = trackName
        };
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;

        // Unsubscribe from PropertyChanged self-subscription
        if (propertyChangedHandler is not null)
        {
            PropertyChanged -= propertyChangedHandler;
        }
    }
}
