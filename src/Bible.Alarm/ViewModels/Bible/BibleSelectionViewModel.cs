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
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public class BibleSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    private BibleReadingSchedule? current;
    private BibleReadingSchedule? tentative;
    private bool initComplete;
    private BibleReadingSchedule? lastCurrent;
    private BibleReadingSchedule? lastTentative;
    private PropertyChangedEventHandler? propertyChangedHandler;

    public ICommand BackCommand { get; set; }
    public ICommand BookSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    public BibleSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;

        // Initialize current and tentative from state if available (map DTOs to entities)
        var currentState = state.Value;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(currentState.CurrentBibleReadingSchedule);
        }
        if (currentState.TentativeBibleReadingSchedule != null)
        {
            tentative = mapper.Map<BibleReadingSchedule>(currentState.TentativeBibleReadingSchedule);
        }

        state.StateChanged += OnBibleReadingInitialized;
        state.StateChanged += OnBibleReadingChanged;

        // Check current state immediately in case state is already set (e.g., when navigating from schedule page)
        if (currentState.CurrentBibleReadingSchedule != null && currentState.TentativeBibleReadingSchedule != null)
        {
            OnBibleReadingInitialized(null, EventArgs.Empty);
        }

        BookSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;
            await this.navigationService.NavigateToBookSelectionAsync();
            // Map entity to DTO before dispatching
            var tentativeItem = new BibleReadingStateItem
            {
                PublicationCode = x.Code,
                LanguageCode = CurrentLanguage?.Code ?? string.Empty
            };
            this.dispatcher.Dispatch(new BookSelectionAction(tentativeItem));
            IsBusy = false;
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await this.navigationService.OpenLanguageModalAsync(this);
            IsBusy = false;
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
            if (CurrentLanguage != null)
            {
                CurrentLanguage.IsSelected = false;
            }

            CurrentLanguage = x;
            CurrentLanguage!.IsSelected = true;

            // Close the modal immediately after language selection
            await this.navigationService.PopModalAsync();

            // Populate translations after closing the modal
            await PopulateTranslations(x.Code);

            IsBusy = false;
        });
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null)
        {
            return;
        }
        // Map DTOs to entities
        current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        tentative = mapper.Map<BibleReadingSchedule>(stateValue.TentativeBibleReadingSchedule);
        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(tentative.LanguageCode);

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following chapter/track selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    private void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null)
        {
            return;
        }

        // Map DTOs to entities
        var newCurrent = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        var newTentative = mapper.Map<BibleReadingSchedule>(stateValue.TentativeBibleReadingSchedule);

        // Compare by ID to avoid unnecessary updates
        if (lastCurrent?.Id == newCurrent.Id && lastTentative?.Id == newTentative.Id)
        {
            return;
        }

        current = newCurrent;
        tentative = newTentative;
        lastCurrent = current;
        lastTentative = tentative;

        // Update selected translation when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedTranslation);
    }

    private void SetSelectedTranslation()
    {
        if (current == null || tentative == null)
        {
            return;
        }

        if (current.LanguageCode != tentative.LanguageCode)
        {
            return;
        }

        if (SelectedTranslation != null)
        {
            SelectedTranslation.IsSelected = false;
        }

        if (!translationVMsMapping.TryGetValue(current.PublicationCode, out var translation))
        {
            return;
        }

        SelectedTranslation = translation;
        SelectedTranslation!.IsSelected = true;
    }

    private ObservableCollection<PublicationListViewItemModel>? translations;

    public ObservableCollection<PublicationListViewItemModel> Translations
    {
        get => translations ??= new ObservableCollection<PublicationListViewItemModel>();
        set => SetProperty(ref translations, value);
    }

    private ObservableCollection<LanguageListViewItemModel>? languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= new ObservableCollection<LanguageListViewItemModel>();
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
        get => tentative?.PublicationCode ?? "";
        set
        {
            if (tentative == null)
            {
                return;
            }

            tentative.PublicationCode = value;
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

            if (tentative == null || languageVm.Code != tentative.LanguageCode)
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

    private async Task PopulateTranslations(string languageCode)
    {
        translationVMsMapping.Clear();

        // Run database operations off UI thread
        var translations = await Task.Run(async () =>
            await mediaService.GetBibleTranslations(languageCode));
        var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var translation in translations.Select(x => x.Value))
        {
            var translationVm = new PublicationListViewItemModel(translation);

            translationVMs.Add(translationVm);
            translationVMsMapping.Add(translationVm.Code, translationVm);

            if (current == null)
            {
                continue;
            }

            if (current.LanguageCode != languageCode
                || current.PublicationCode != translation.Code)
            {
                continue;
            }

            translationVm.IsSelected = true;
            SelectedTranslation = translationVm;
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
