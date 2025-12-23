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
                // Ensure current is set from state if it's null
                if (current == null)
                {
                    var currentItem = state.Value.CurrentMusic;
                    if (currentItem != null)
                    {
                        current = mapper.Map<AlarmMusic>(currentItem);
                    }
                }

                var languageCode = CurrentLanguage?.Code ?? string.Empty;
                if (string.IsNullOrEmpty(languageCode))
                {
                    return;
                }

                // Get tracks for the selected song book and language
                var tracks = await Task.Run(async () =>
                    await mediaService.GetVocalMusicTracks(languageCode, x.Code));

                if (tracks == null || tracks.Count == 0)
                {
                    return;
                }

                // Get a random track (instead of first track for cascade changes)
                var tracksList = tracks.Values.ToList();
                var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                var randomTrackNumber = randomTrack.Number;

                // Create MusicStateItem with selected song book and random track
                // IMPORTANT: Include display names from list items (no database query needed)
                var trackSelectedItem = new MusicStateItem
                {
                    Repeat = current?.Repeat ?? false,
                    MusicType = MusicType.Vocals,
                    LanguageCode = languageCode,
                    PublicationCode = x.Code,
                    TrackNumber = randomTrackNumber,
                    // Store display names from list items
                    LanguageName = CurrentLanguage?.Name,
                    PublicationName = x.Name,
                    TrackName = randomTrack.Title
                };

                // Dispatch TrackSelectedAction to update CurrentMusic
                dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

                // Navigate back to schedule page
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
                                LanguageCode = currentSchedule.MusicLanguageCode,
                                PublicationCode = currentSchedule.MusicPublicationCode,
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
                // Ensure current is set from state if it's null
                if (current == null)
                {
                    var currentItem = state.Value.CurrentMusic;
                    if (currentItem != null)
                    {
                        current = mapper.Map<AlarmMusic>(currentItem);
                    }
                }

                if (CurrentLanguage != null)
                {
                    CurrentLanguage.IsSelected = false;
                }

                CurrentLanguage = x;
                CurrentLanguage!.IsSelected = true;

                var languageCode = x.Code;

                // Get the first song book for the selected language
                var songBooks = await Task.Run(async () =>
                    await mediaService.GetVocalMusicReleases(languageCode));

                if (songBooks == null || songBooks.Count == 0)
                {
                    IsBusy = false;
                    return;
                }

                // Get the first song book (first in dictionary)
                var firstSongBook = songBooks.FirstOrDefault();
                if (firstSongBook.Value == null)
                {
                    IsBusy = false;
                    return;
                }

                var publicationCode = firstSongBook.Key;

                // Get tracks for the selected song book and language
                var tracks = await Task.Run(async () =>
                    await mediaService.GetVocalMusicTracks(languageCode, publicationCode));

                if (tracks == null || tracks.Count == 0)
                {
                    IsBusy = false;
                    return;
                }

                // Get a random track (instead of first track for cascade changes)
                var tracksList = tracks.Values.ToList();
                var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                var randomTrackNumber = randomTrack.Number;

                // Create MusicStateItem with selected language, first song book, and random track
                // IMPORTANT: Include display names from list items (no database query needed)
                var trackSelectedItem = new MusicStateItem
                {
                    Repeat = current?.Repeat ?? false,
                    MusicType = MusicType.Vocals,
                    LanguageCode = languageCode,
                    PublicationCode = publicationCode,
                    TrackNumber = randomTrackNumber,
                    // Store display names from list items
                    LanguageName = x.Name,
                    PublicationName = firstSongBook.Value.Name,
                    TrackName = randomTrack.Title
                };

                // Dispatch TrackSelectedAction to update CurrentMusic
                dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

                // Close the modal and navigate back to schedule page
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
                PublicationCode = currentSchedule.MusicPublicationCode,
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
                PublicationCode = currentSchedule.MusicPublicationCode,
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

    private bool isBusy;

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
