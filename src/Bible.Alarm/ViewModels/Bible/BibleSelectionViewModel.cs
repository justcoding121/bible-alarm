#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public sealed class BibleSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    private BibleReadingSchedule? current;
    private bool initComplete;
    private BibleReadingSchedule? lastCurrent;
    private PropertyChangedEventHandler? propertyChangedHandler;

    // Track last language code to detect changes
    private string? lastLanguageCode;

    public ICommand BackCommand { get; set; }
    public ICommand BookSelectionCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    public BibleSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;

        // Initialize current from state if available (map DTO to entity)
        var currentState = state.Value;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(currentState.CurrentBibleReadingSchedule);
            lastLanguageCode = current.LanguageCode;
        }

        state.StateChanged += OnBibleReadingInitialized;
        state.StateChanged += OnBibleReadingChanged;

        // Always trigger initialization, even if CurrentBibleReadingSchedule is null
        // This ensures languages are populated for the language modal use case
        OnBibleReadingInitialized(null, EventArgs.Empty);

        BookSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x == null || CurrentLanguage == null)
            {
                return;
            }

            var (bookNumber, chapterNumber, bookName) = await GetBookAndChapterForTranslationAsync(x);
            if (bookNumber == 0)
            {
                return;
            }

            var bibleReadingItem = CreateBibleReadingItemFromSelection(x, bookNumber, chapterNumber, bookName);
            DispatchBibleReadingSelectionActions(bibleReadingItem);
            await this.navigationService.PopModalAsync();
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await this.navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await this.navigationService.PopModalAsync();
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            UpdateSelectedLanguage(x);
            await this.navigationService.PopModalAsync();

            var (publicationCode, bookNumber, chapterNumber, bookName, publicationName) =
                await GetTranslationBookAndChapterForLanguageAsync(x);

            if (publicationCode == null)
            {
                return;
            }

            var bibleReadingItem = CreateBibleReadingItemForLanguageSelection(
                x, publicationCode, bookNumber, chapterNumber, bookName, publicationName);
            DispatchLanguageSelectionActions(bibleReadingItem, x);
        });
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        string? newLanguageCode = null;
        if (stateValue.CurrentSchedule != null)
        {
            newLanguageCode = stateValue.CurrentSchedule.BibleReadingLanguageCode;
        }

        // Update tracking variable
        if (!string.IsNullOrEmpty(newLanguageCode))
        {
            lastLanguageCode = newLanguageCode;
        }

        // Update current if we have CurrentBibleReadingSchedule (for other properties like PublicationCode)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            if (string.IsNullOrEmpty(lastLanguageCode))
            {
                lastLanguageCode = current.LanguageCode;
            }
        }
        else if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(newLanguageCode))
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = stateValue.CurrentSchedule.BibleReadingPublicationCode,
                BookNumber = stateValue.CurrentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = stateValue.CurrentSchedule.BibleReadingChapterNumber ?? 1
            };
        }

        initComplete = true;

        Task.Run(async () =>
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);

                if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
                {
                    // Initialize with current language code if we have a current schedule
                    await Initialize(current.LanguageCode);
                }
                else
                {
                    // For language modal use case, just populate languages without translations
                    await PopulateLanguages();

                    // Subscribe to LanguageSearchTerm property changes
                    propertyChangedHandler = (sender, e) =>
                    {
                        if (e.PropertyName == "LanguageSearchTerm")
                        {
                            _ = PopulateLanguages(LanguageSearchTerm?.Trim());
                        }
                    };
                    PropertyChanged += propertyChangedHandler;
                }

                // CollectionView needs a moment to render before hiding the busy indicator
                // Add a small delay to prevent blank page flash (following chapter/track selection pattern)
                // Give CollectionView time to render
                await Task.Delay(100);

                // Set IsBusy to false after collection is assigned and rendered
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
            catch (Exception ex)
            {
                // Always reset IsBusy even if initialization fails
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                // Log error but don't throw - allow modal to open even if initialization fails
#if DEBUG
                Log.Error(ex, "Error initializing BibleSelectionViewModel");
#endif
            }
        });
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures translations are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;

        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Update tracking variable
        lastLanguageCode = newLanguageCode;

        // Update current from CurrentSchedule
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        }
        else if (!string.IsNullOrEmpty(newLanguageCode))
        {
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode,
                BookNumber = currentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
        }

        // Ensure translations are populated if not already initialized
        if (!initComplete || Translations == null || Translations.Count == 0)
        {
            initComplete = true;
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                await PopulateTranslations(current.LanguageCode);
            }
            await Task.Delay(100); // Give CollectionView time to render
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;

        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Check if language code changed (need to repopulate translations)
        var languageChanged = lastLanguageCode != newLanguageCode;

        // If no changes detected and we're already initialized, skip
        if (!languageChanged && initComplete)
        {
            return;
        }

        // Update tracking variable
        lastLanguageCode = newLanguageCode;

        // Update current if we have CurrentBibleReadingSchedule (for other properties like PublicationCode)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode,
                BookNumber = currentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            lastCurrent = current;
        }

        // If language changed, repopulate translations
        if (languageChanged && initComplete)
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                    // Clear the mapping dictionary before repopulating to avoid duplicate key errors
                    translationVMsMapping.Clear();
                    // Pass languageChanged flag to PopulateTranslations so it can select default translation
                    await PopulateTranslations(newLanguageCode, languageChanged: true);
                    await Task.Delay(100); // Give CollectionView time to render
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - allow modal to continue functioning
#if DEBUG
                    Log.Error(ex, "Error repopulating translations in OnBibleReadingChanged");
#endif
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                }
            });
        }
        else
        {
            // Update selected translation when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedTranslation);
        }
    }

    private void SetSelectedTranslation()
    {
        // Use CurrentSchedule as the source of truth for publication code
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var publicationCode = stateValue.CurrentSchedule.BibleReadingPublicationCode;
        if (string.IsNullOrEmpty(publicationCode))
        {
            return;
        }

        if (SelectedTranslation != null)
        {
            SelectedTranslation.IsSelected = false;
        }

        if (!translationVMsMapping.TryGetValue(publicationCode, out var translation))
        {
            return;
        }

        SelectedTranslation = translation;
        SelectedTranslation!.IsSelected = true;
    }

    private ObservableCollection<PublicationListViewItemModel>? translations;

    public ObservableCollection<PublicationListViewItemModel> Translations
    {
        get => translations ??= [];
        set => SetProperty(ref translations, value);
    }

    private ObservableCollection<LanguageListViewItemModel>? languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public PublicationListViewItemModel? SelectedTranslation { get; set; }

    private LanguageListViewItemModel? currentLanguage;

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public string PublicationCode
    {
        get => current?.PublicationCode ?? "";
        set
        {
            if (current == null)
            {
                return;
            }

            current.PublicationCode = value;
            OnPropertyChanged();
        }
    }

    private string languageSearchTerm = string.Empty;

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public object? SelectedItem => CurrentLanguage;

    private async Task Initialize(string languageCode)
    {
        await PopulateLanguages();
        await PopulateTranslations(languageCode);

        // Subscribe to LanguageSearchTerm property changes
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
            await mediaService.GetBibleLanguages());
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

            // Use CurrentSchedule as the source of truth for language code
            var stateValue = state.Value;
            var currentLanguageCode = stateValue.CurrentSchedule?.BibleReadingLanguageCode;

            if (string.IsNullOrEmpty(currentLanguageCode) || languageVm.Code != currentLanguageCode)
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

    private readonly Dictionary<string, PublicationListViewItemModel> translationVMsMapping = [];

    private async Task PopulateTranslations(string languageCode, bool languageChanged = false)
    {
        translationVMsMapping.Clear();

        // Run database operations off UI thread
        var translations = await Task.Run(async () =>
            await mediaService.GetBibleTranslations(languageCode));
        var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

        // Use CurrentSchedule as the source of truth for publication code
        var stateValue = state.Value;
        var currentPublicationCode = stateValue.CurrentSchedule?.BibleReadingPublicationCode;

        PublicationListViewItemModel? defaultTranslation = null;

        foreach (var translation in translations.Select(x => x.Value))
        {
            // Skip duplicates - if code already exists, use the existing one
            if (translationVMsMapping.TryGetValue(translation.Code, out var existingVm))
            {
                // Still check if this duplicate matches the current publication code
                if (!string.IsNullOrEmpty(currentPublicationCode)
                    && currentPublicationCode == translation.Code)
                {
                    existingVm.IsSelected = true;
                    SelectedTranslation = existingVm;
                }
                continue;
            }

            var translationVm = new PublicationListViewItemModel(translation);

            translationVMs.Add(translationVm);
            translationVMsMapping[translationVm.Code] = translationVm;

            // Store the last translation (reverse order) as default
            defaultTranslation = translationVm;

            // Check if this translation matches the current publication code from CurrentSchedule
            if (!string.IsNullOrEmpty(currentPublicationCode)
                && currentPublicationCode == translation.Code)
            {
                translationVm.IsSelected = true;
                SelectedTranslation = translationVm;
            }
        }

        // If language changed and no translation matches current publication code, select the last one (reverse order)
        if (languageChanged && SelectedTranslation == null && defaultTranslation != null)
        {
            defaultTranslation.IsSelected = true;
            SelectedTranslation = defaultTranslation;

            // Dispatch action to update state with the default translation
            // Get the first book and chapter for the default translation
            _ = Task.Run(async () =>
            {
                try
                {
                    var books = await mediaService.GetBibleBooks(languageCode, defaultTranslation.Code);
                    if (books != null && books.Count > 0)
                    {
                        var firstBook = books.Values.First();
                        var chapters = await mediaService.GetBibleChapters(languageCode, defaultTranslation.Code, firstBook.Number);
                        if (chapters != null && chapters.Count > 0)
                        {
                            var firstChapter = chapters.Values.First();
                            var bibleReadingItem = new BibleReadingStateItem
                            {
                                LanguageCode = languageCode,
                                PublicationCode = defaultTranslation.Code,
                                BookNumber = firstBook.Number,
                                ChapterNumber = firstChapter.Number,
                                LanguageName = stateValue.CurrentSchedule?.BibleReadingLanguageName,
                                PublicationName = defaultTranslation.Name,
                                BookName = firstBook.Name
                            };
                            dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "BibleSelectionViewModel: Error dispatching default translation selection");
                }
            });
        }

        // Assign collection on main thread to ensure UI updates before IsBusy is set to false
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Translations = translationVMs;
        });
    }

    private async Task<(int BookNumber, int ChapterNumber, string BookName)> GetBookAndChapterForTranslationAsync(PublicationListViewItemModel publication)
    {
        var currentSchedule = this.state.Value.CurrentSchedule;
        var isSameTranslation = IsSameTranslation(currentSchedule, publication);

        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(CurrentLanguage!.Code, publication.Code));

        if (books == null || books.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        if (isSameTranslation && HasValidBookAndChapter(currentSchedule))
        {
            return await GetPreservedBookAndChapterAsync(currentSchedule!, publication, books);
        }

        return await GetFirstBookAndChapterAsync(publication, books);
    }

    private static bool IsSameTranslation(ScheduleStateItem? currentSchedule, PublicationListViewItemModel publication)
    {
        return currentSchedule != null &&
               currentSchedule.BibleReadingLanguageCode == publication.Code &&
               currentSchedule.BibleReadingPublicationCode == publication.Code;
    }

    private static bool HasValidBookAndChapter(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.BibleReadingBookNumber.HasValue &&
               schedule.BibleReadingChapterNumber.HasValue;
    }

    private async Task<(int BookNumber, int ChapterNumber, string BookName)> GetPreservedBookAndChapterAsync(
        ScheduleStateItem currentSchedule,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleBook> books)
    {
        var currentBookNumber = currentSchedule.BibleReadingBookNumber!.Value;
        var currentChapterNumber = currentSchedule.BibleReadingChapterNumber!.Value;

        if (books.TryGetValue(currentBookNumber, out var currentBook))
        {
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(CurrentLanguage!.Code, publication.Code, currentBookNumber));

            var chapterNumber = chapters != null && chapters.ContainsKey(currentChapterNumber)
                ? currentChapterNumber
                : chapters?.Values.First().Number ?? 1;

            return (currentBookNumber, chapterNumber, currentBook.Name);
        }

        return await GetFirstBookAndChapterAsync(publication, books);
    }

    private async Task<(int BookNumber, int ChapterNumber, string BookName)> GetFirstBookAndChapterAsync(
        PublicationListViewItemModel publication,
        IDictionary<int, BibleBook> books)
    {
        var firstBook = books.Values.First();
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(CurrentLanguage!.Code, publication.Code, firstBook.Number));

        if (chapters == null || chapters.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        return (firstBook.Number, chapters.Values.First().Number, firstBook.Name);
    }

    private BibleReadingStateItem CreateBibleReadingItemFromSelection(
        PublicationListViewItemModel publication,
        int bookNumber,
        int chapterNumber,
        string bookName)
    {
        return new BibleReadingStateItem
        {
            PublicationCode = publication.Code,
            LanguageCode = CurrentLanguage!.Code,
            BookNumber = bookNumber,
            ChapterNumber = chapterNumber,
            LanguageName = CurrentLanguage.Name,
            PublicationName = publication.Name,
            BookName = bookName
        };
    }

    private void DispatchBibleReadingSelectionActions(BibleReadingStateItem bibleReadingItem)
    {
        this.dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));
        this.dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
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

    private async Task<(string? PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetTranslationBookAndChapterForLanguageAsync(LanguageListViewItemModel language)
    {
        var currentSchedule = this.state.Value.CurrentSchedule;
        var isSameLanguage = currentSchedule != null &&
                           currentSchedule.BibleReadingLanguageCode == language.Code;

        var translations = await Task.Run(async () =>
            await mediaService.GetBibleTranslations(language.Code));

        if (translations == null || translations.Count == 0)
        {
            return (null, 0, 0, string.Empty, string.Empty);
        }

        if (isSameLanguage && CanPreserveCurrentTranslation(currentSchedule!, translations))
        {
            return await GetPreservedTranslationBookAndChapterAsync(currentSchedule!, language, translations);
        }

        return await GetDefaultTranslationBookAndChapterAsync(language, translations);
    }

    private static bool CanPreserveCurrentTranslation(ScheduleStateItem schedule, Dictionary<string, BibleTranslation> translations)
    {
        return !string.IsNullOrEmpty(schedule.BibleReadingPublicationCode) &&
               translations.ContainsKey(schedule.BibleReadingPublicationCode) &&
               schedule.BibleReadingBookNumber.HasValue &&
               schedule.BibleReadingChapterNumber.HasValue;
    }

    private async Task<(string PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetPreservedTranslationBookAndChapterAsync(
            ScheduleStateItem currentSchedule,
            LanguageListViewItemModel language,
            Dictionary<string, BibleTranslation> translations)
    {
        var publicationCode = currentSchedule.BibleReadingPublicationCode!;
        var currentBookNumber = currentSchedule.BibleReadingBookNumber!.Value;
        var currentChapterNumber = currentSchedule.BibleReadingChapterNumber!.Value;

        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(language.Code, publicationCode));

        if (books != null && books.TryGetValue(currentBookNumber, out var currentBook))
        {
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(language.Code, publicationCode, currentBookNumber));

            var chapterNumber = chapters != null && chapters.ContainsKey(currentChapterNumber)
                ? currentChapterNumber
                : chapters?.Values.First().Number ?? 1;

            var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
            return (publicationCode, currentBookNumber, chapterNumber, currentBook.Name, publicationName);
        }

        return await GetFirstBookForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetDefaultTranslationBookAndChapterAsync(
            LanguageListViewItemModel language,
            Dictionary<string, BibleTranslation> translations)
    {
        var lastTranslation = translations.LastOrDefault();
        if (lastTranslation.Value == null)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationCode = lastTranslation.Key;
        return await GetFirstBookForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetFirstBookForTranslationAsync(
            LanguageListViewItemModel language,
            string publicationCode,
            Dictionary<string, BibleTranslation> translations)
    {
        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(language.Code, publicationCode));

        if (books == null || books.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var firstBook = books.Values.First();
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(language.Code, publicationCode, firstBook.Number));

        if (chapters == null || chapters.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
        return (publicationCode, firstBook.Number, chapters.Values.First().Number, firstBook.Name, publicationName);
    }

    private BibleReadingStateItem CreateBibleReadingItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        int bookNumber,
        int chapterNumber,
        string bookName,
        string publicationName)
    {
        return new BibleReadingStateItem
        {
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            BookNumber = bookNumber,
            ChapterNumber = chapterNumber,
            LanguageName = language.Name,
            PublicationName = publicationName,
            BookName = bookName
        };
    }

    private void DispatchLanguageSelectionActions(BibleReadingStateItem bibleReadingItem, LanguageListViewItemModel language)
    {
        Log.Information("BibleSelectionViewModel: SelectLanguageCommand - Selected language: {LanguageName} ({LanguageCode}), Translation: {PublicationCode}, Book: {BookNumber}, Chapter: {ChapterNumber}",
            language.Name, language.Code, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);

        Log.Debug("BibleSelectionViewModel: SelectLanguageCommand - Dispatching ChapterSelectedAction with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
            bibleReadingItem.LanguageCode, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);
        this.dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

        Log.Debug("BibleSelectionViewModel: SelectLanguageCommand - Dispatching BibleSelectionAction");
        this.dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
    }

    public void Dispose()
    {
        state.StateChanged -= OnBibleReadingInitialized;
        state.StateChanged -= OnBibleReadingChanged;

        // Unsubscribe from PropertyChanged self-subscription
        if (propertyChangedHandler is not null)
        {
            PropertyChanged -= propertyChangedHandler;
        }
    }
}
