#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
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

            IsBusy = true;

            try
            {
                // Check if the selected translation is the same as the current translation
                var currentSchedule = this.state.Value.CurrentSchedule;
                var isSameTranslation = currentSchedule != null && 
                                       currentSchedule.BibleReadingLanguageCode == CurrentLanguage.Code &&
                                       currentSchedule.BibleReadingPublicationCode == x.Code;

                // Get books for the selected translation
                var books = await Task.Run(async () =>
                    await mediaService.GetBibleBooks(CurrentLanguage.Code, x.Code));

                if (books == null || books.Count == 0)
                {
                    IsBusy = false;
                    return;
                }

                // If it's the same translation, preserve the current book and chapter (if valid)
                // Otherwise, use the first book and first chapter
                int bookNumber;
                int chapterNumber;
                string bookName;
                
                if (isSameTranslation && 
                    currentSchedule.BibleReadingBookNumber.HasValue && 
                    currentSchedule.BibleReadingChapterNumber.HasValue)
                {
                    var currentBookNumber = currentSchedule.BibleReadingBookNumber.Value;
                    var currentChapterNumber = currentSchedule.BibleReadingChapterNumber.Value;
                    
                    // Verify the current book exists in the books list
                    if (books.TryGetValue(currentBookNumber, out var currentBook))
                    {
                        bookNumber = currentBookNumber;
                        bookName = currentBook.Name;
                        
                        // Verify the current chapter exists in this book
                        var chapters = await Task.Run(async () =>
                            await mediaService.GetBibleChapters(CurrentLanguage.Code, x.Code, bookNumber));
                        
                        if (chapters != null && chapters.ContainsKey(currentChapterNumber))
                        {
                            chapterNumber = currentChapterNumber;
                            System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: BookSelectionCommand - Same translation selected ({x.Name}), preserving current book {bookNumber} and chapter {chapterNumber}");
                        }
                        else
                        {
                            // Current chapter doesn't exist, use first chapter
                            chapterNumber = chapters?.Values.First().Number ?? 1;
                            System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: BookSelectionCommand - Same translation selected ({x.Name}), preserving book {bookNumber}, but current chapter {currentChapterNumber} doesn't exist, using first chapter {chapterNumber}");
                        }
                    }
                    else
                    {
                        // Current book doesn't exist, use first book and first chapter
                        var firstBook = books.Values.First();
                        bookNumber = firstBook.Number;
                        bookName = firstBook.Name;
                        
                        var chapters = await Task.Run(async () =>
                            await mediaService.GetBibleChapters(CurrentLanguage.Code, x.Code, bookNumber));
                        
                        chapterNumber = chapters?.Values.First().Number ?? 1;
                        System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: BookSelectionCommand - Same translation selected ({x.Name}), but current book {currentBookNumber} doesn't exist, using first book {bookNumber} and first chapter {chapterNumber}");
                    }
                }
                else
                {
                    // Different translation selected, use first book and first chapter
                    var firstBook = books.Values.First();
                    bookNumber = firstBook.Number;
                    bookName = firstBook.Name;
                    
                    var chapters = await Task.Run(async () =>
                        await mediaService.GetBibleChapters(CurrentLanguage.Code, x.Code, bookNumber));
                    
                    if (chapters == null || chapters.Count == 0)
                    {
                        IsBusy = false;
                        return;
                    }
                    
                    chapterNumber = chapters.Values.First().Number;
                    System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: BookSelectionCommand - Different translation selected ({x.Name}, was {currentSchedule?.BibleReadingPublicationCode ?? "null"}), using first book {bookNumber} and first chapter {chapterNumber}");
                }

                // Create BibleReadingStateItem with selected translation, book, and chapter
                // IMPORTANT: Include display names from list items (no database query needed)
                var bibleReadingItem = new BibleReadingStateItem
                {
                    PublicationCode = x.Code,
                    LanguageCode = CurrentLanguage.Code,
                    BookNumber = bookNumber,
                    ChapterNumber = chapterNumber,
                    // Store display names from list items
                    LanguageName = CurrentLanguage.Name,
                    PublicationName = x.Name,
                    BookName = bookName
                };

                // Dispatch ChapterSelectedAction to update CurrentBibleReadingSchedule
                // Effect will automatically sync to CurrentSchedule
                this.dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

                // Also update current state
                this.dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));

                // Close modal and navigate back to schedule page
                await this.navigationService.PopModalAsync();
            }
            finally
            {
                IsBusy = false;
            }
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await this.navigationService.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await this.navigationService.PopModalAsync();
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
                if (CurrentLanguage != null)
                {
                    CurrentLanguage.IsSelected = false;
                }

                CurrentLanguage = x;
                CurrentLanguage!.IsSelected = true;

                // Close the modal immediately after language selection
                await this.navigationService.PopModalAsync();

                // Check if the selected language is the same as the current language
                var currentSchedule = this.state.Value.CurrentSchedule;
                var isSameLanguage = currentSchedule != null &&
                                   currentSchedule.BibleReadingLanguageCode == x.Code;

                // Get translations for the selected language
                var translations = await Task.Run(async () =>
                    await mediaService.GetBibleTranslations(x.Code));

                if (translations == null || translations.Count == 0)
                {
                    IsBusy = false;
                    return;
                }

                // If it's the same language, try to preserve the current translation, book, and chapter (if valid)
                // Otherwise, use the last translation (reverse order), first book, and first chapter
                string publicationCode;
                int bookNumber;
                int chapterNumber;
                string bookName;
                
                if (isSameLanguage && 
                    !string.IsNullOrEmpty(currentSchedule.BibleReadingPublicationCode) &&
                    translations.ContainsKey(currentSchedule.BibleReadingPublicationCode) &&
                    currentSchedule.BibleReadingBookNumber.HasValue &&
                    currentSchedule.BibleReadingChapterNumber.HasValue)
                {
                    // Same language - try to preserve current translation, book, and chapter
                    publicationCode = currentSchedule.BibleReadingPublicationCode;
                    var currentBookNumber = currentSchedule.BibleReadingBookNumber.Value;
                    var currentChapterNumber = currentSchedule.BibleReadingChapterNumber.Value;
                    
                    // Verify the current book exists in the books list
                    var books = await Task.Run(async () =>
                        await mediaService.GetBibleBooks(x.Code, publicationCode));
                    
                    if (books != null && books.TryGetValue(currentBookNumber, out var currentBook))
                    {
                        bookNumber = currentBookNumber;
                        bookName = currentBook.Name;
                        
                        // Verify the current chapter exists in this book
                        var chapters = await Task.Run(async () =>
                            await mediaService.GetBibleChapters(x.Code, publicationCode, bookNumber));
                        
                        if (chapters != null && chapters.ContainsKey(currentChapterNumber))
                        {
                            chapterNumber = currentChapterNumber;
                            System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: SelectLanguageCommand - Same language selected ({x.Name}), preserving current translation {publicationCode}, book {bookNumber}, and chapter {chapterNumber}");
                        }
                        else
                        {
                            // Current chapter doesn't exist, use first chapter
                            chapterNumber = chapters?.Values.First().Number ?? 1;
                            System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: SelectLanguageCommand - Same language selected ({x.Name}), preserving translation {publicationCode} and book {bookNumber}, but current chapter {currentChapterNumber} doesn't exist, using first chapter {chapterNumber}");
                        }
                    }
                    else
                    {
                        // Current book doesn't exist, use first book and first chapter
                        var firstBook = books?.Values.First();
                        if (firstBook == null)
                        {
                            IsBusy = false;
                            return;
                        }
                        bookNumber = firstBook.Number;
                        bookName = firstBook.Name;
                        
                        var chapters = await Task.Run(async () =>
                            await mediaService.GetBibleChapters(x.Code, publicationCode, bookNumber));
                        
                        chapterNumber = chapters?.Values.First().Number ?? 1;
                        System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: SelectLanguageCommand - Same language selected ({x.Name}), preserving translation {publicationCode}, but current book {currentBookNumber} doesn't exist, using first book {bookNumber} and first chapter {chapterNumber}");
                    }
                }
                else
                {
                    // Different language selected, use last translation (reverse order), first book, and first chapter
                    var lastTranslation = translations.LastOrDefault();
                    if (lastTranslation.Value == null)
                    {
                        IsBusy = false;
                        return;
                    }
                    
                    publicationCode = lastTranslation.Key;
                    
                    var books = await Task.Run(async () =>
                        await mediaService.GetBibleBooks(x.Code, publicationCode));
                    
                    if (books == null || books.Count == 0)
                    {
                        IsBusy = false;
                        return;
                    }
                    
                    var firstBook = books.Values.First();
                    bookNumber = firstBook.Number;
                    bookName = firstBook.Name;
                    
                    var chapters = await Task.Run(async () =>
                        await mediaService.GetBibleChapters(x.Code, publicationCode, bookNumber));
                    
                    if (chapters == null || chapters.Count == 0)
                    {
                        IsBusy = false;
                        return;
                    }
                    
                    chapterNumber = chapters.Values.First().Number;
                    System.Diagnostics.Debug.WriteLine($"BibleSelectionViewModel: SelectLanguageCommand - Different language selected ({x.Name}, was {currentSchedule?.BibleReadingLanguageCode ?? "null"}), using last translation {publicationCode}, first book {bookNumber}, and first chapter {chapterNumber}");
                }

                // Get publication name from translations
                var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;

                // Create BibleReadingStateItem with selected language, translation, book, and chapter
                // IMPORTANT: Include display names from list items (no database query needed)
                var bibleReadingItem = new BibleReadingStateItem
                {
                    LanguageCode = x.Code,
                    PublicationCode = publicationCode,
                    BookNumber = bookNumber,
                    ChapterNumber = chapterNumber,
                    // Store display names from list items
                    LanguageName = x.Name,
                    PublicationName = publicationName,
                    BookName = bookName
                };

                Log.Information("BibleSelectionViewModel: SelectLanguageCommand - Selected language: {LanguageName} ({LanguageCode}), Translation: {PublicationCode}, Book: {BookNumber}, Chapter: {ChapterNumber}",
                    x.Name, x.Code, publicationCode, bookNumber, chapterNumber);

                // Dispatch ChapterSelectedAction to update CurrentBibleReadingSchedule
                // Effect will automatically sync to CurrentSchedule (with shouldSave: false)
                Log.Debug("BibleSelectionViewModel: SelectLanguageCommand - Dispatching ChapterSelectedAction with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
                    bibleReadingItem.LanguageCode, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);
                this.dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

                // Also update current state
                Log.Debug("BibleSelectionViewModel: SelectLanguageCommand - Dispatching BibleSelectionAction");
                this.dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
            }
            finally
            {
                IsBusy = false;
            }
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
                System.Diagnostics.Debug.WriteLine($"Error initializing BibleSelectionViewModel: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"Error repopulating translations in OnBibleReadingChanged: {ex.Message}");
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
            var translationVm = new PublicationListViewItemModel(translation);

            translationVMs.Add(translationVm);
            translationVMsMapping.Add(translationVm.Code, translationVm);

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
