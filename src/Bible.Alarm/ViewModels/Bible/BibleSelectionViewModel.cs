#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public class BibleSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IMediaService _mediaService;
    private readonly IState<ApplicationState> _state;
    private readonly IDispatcher _dispatcher;
    private readonly INavigationService _navigationService;

    private BibleReadingSchedule? _current;
    private BibleReadingSchedule? _tentative;
    private bool _initComplete;
    private BibleReadingSchedule? _lastCurrent;
    private BibleReadingSchedule? _lastTentative;
    private PropertyChangedEventHandler? _propertyChangedHandler;

    public ICommand BackCommand { get; set; }
    public ICommand BookSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    public BibleSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService)
    {
        _mediaService = mediaService;
        _state = state;
        _dispatcher = dispatcher;
        _navigationService = navigationService;

        // Initialize _current and _tentative from state if available
        var currentState = _state.Value;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            _current = currentState.CurrentBibleReadingSchedule;
        }
        if (currentState.TentativeBibleReadingSchedule != null)
        {
            _tentative = currentState.TentativeBibleReadingSchedule;
        }

        _state.StateChanged += OnBibleReadingInitialized;
        _state.StateChanged += OnBibleReadingChanged;
        
        // Check current state immediately in case state is already set (e.g., when navigating from schedule page)
        if (currentState.CurrentBibleReadingSchedule != null && currentState.TentativeBibleReadingSchedule != null)
        {
            OnBibleReadingInitialized(null, EventArgs.Empty);
        }

        BookSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x == null) return;
            
            IsBusy = true;
            await navigationService.NavigateToBookSelectionAsync();
            dispatcher.Dispatch(new BookSelectionAction(new BibleReadingSchedule
            {
                PublicationCode = x.Code,
                LanguageCode = CurrentLanguage?.Code ?? string.Empty
            }));
            IsBusy = false;
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.OpenLanguageModalAsync(this);
            IsBusy = false;
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
            if (x == null) return;
            
            IsBusy = true;
            if (CurrentLanguage != null) CurrentLanguage.IsSelected = false;

            CurrentLanguage = x;
            CurrentLanguage!.IsSelected = true;

            // Close the modal immediately after language selection
            await _navigationService.PopModalAsync();

            // Populate translations after closing the modal
            await PopulateTranslations(x.Code);

            IsBusy = false;
        });
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        if (_initComplete) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null) return;
        _current = stateValue.CurrentBibleReadingSchedule;
        _tentative = stateValue.TentativeBibleReadingSchedule;
        _initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(_tentative.LanguageCode);
            
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
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null 
            || (stateValue.CurrentBibleReadingSchedule == _lastCurrent && stateValue.TentativeBibleReadingSchedule == _lastTentative)) return;
        _current = stateValue.CurrentBibleReadingSchedule;
        _tentative = stateValue.TentativeBibleReadingSchedule;
        _lastCurrent = _current;
        _lastTentative = _tentative;
        
        // Update selected translation when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedTranslation);
    }

    private void SetSelectedTranslation()
    {
        if (_current == null || _tentative == null) return;
        if (_current.LanguageCode != _tentative.LanguageCode) return;
        if (SelectedTranslation != null) SelectedTranslation.IsSelected = false;

        if (!_translationVMsMapping.TryGetValue(_current.PublicationCode, out var translation)) return;
        
        SelectedTranslation = translation;
        SelectedTranslation!.IsSelected = true;
    }

    private ObservableCollection<PublicationListViewItemModel>? _translations;

    public ObservableCollection<PublicationListViewItemModel> Translations
    {
        get => _translations ??= new ObservableCollection<PublicationListViewItemModel>();
        set => SetProperty(ref _translations, value);
    }

    private ObservableCollection<LanguageListViewItemModel>? _languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => _languages ??= new ObservableCollection<LanguageListViewItemModel>();
        set => SetProperty(ref _languages, value);
    }

    public PublicationListViewItemModel? SelectedTranslation { get; set; }

    private LanguageListViewItemModel? _currentLanguage;

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => _currentLanguage;
        set => SetProperty(ref _currentLanguage, value);
    }

    // Start as true to show busy indicator immediately
    private bool _isBusy = true;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string PublicationCode
    {
        get => _tentative?.PublicationCode ?? "";
        set
        {
            if (_tentative == null) return;
            _tentative.PublicationCode = value;
            OnPropertyChanged();
        }
    }

    private string _languageSearchTerm = string.Empty;

    public string LanguageSearchTerm
    {
        get => _languageSearchTerm;
        set => SetProperty(ref _languageSearchTerm, value);
    }

    public object? SelectedItem => CurrentLanguage;

    private async Task Initialize(string languageCode)
    {
        await PopulateLanguages();
        await PopulateTranslations(languageCode);

        // Subscribe to LanguageSearchTerm property changes
        _propertyChangedHandler = (sender, e) =>
        {
            if (e.PropertyName == "LanguageSearchTerm")
            {
                _ = PopulateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += _propertyChangedHandler;
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        // Run database operations off UI thread
        var languages = await Task.Run(async () =>
            await _mediaService.GetBibleLanguages());
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

            if (_tentative == null || languageVm.Code != _tentative.LanguageCode) continue;
            languageVm.IsSelected = true;
            CurrentLanguage = languageVm;
        }

        // Assign collection on main thread to ensure UI updates
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Languages = languageVMs;
        });
    }

    private readonly Dictionary<string, PublicationListViewItemModel> _translationVMsMapping = [];

    private async Task PopulateTranslations(string languageCode)
    {
        _translationVMsMapping.Clear();

        // Run database operations off UI thread
        var translations = await Task.Run(async () =>
            await _mediaService.GetBibleTranslations(languageCode));
        var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var translation in translations.Select(x => x.Value))
        {
            var translationVm = new PublicationListViewItemModel(translation);

            translationVMs.Add(translationVm);
            _translationVMsMapping.Add(translationVm.Code, translationVm);

            if (_current == null) continue;
            if (_current.LanguageCode != languageCode
                || _current.PublicationCode != translation.Code) continue;
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
        _state.StateChanged -= OnBibleReadingInitialized;
        _state.StateChanged -= OnBibleReadingChanged;
        
        // Unsubscribe from PropertyChanged self-subscription
        if (_propertyChangedHandler is not null)
        {
            PropertyChanged -= _propertyChangedHandler;
        }
    }
}