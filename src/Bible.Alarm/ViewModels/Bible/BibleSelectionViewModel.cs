using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels.Bible;

public class BibleSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly MediaService _mediaService;
    private readonly INavigationService _navigationService;
    private readonly IServiceScopeFactory _scopeFactory;

    private BibleReadingSchedule _current;
    private BibleReadingSchedule _tentative;

    private readonly List<IDisposable> _subscriptions = [];

    public ICommand BackCommand { get; set; }
    public ICommand BookSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }
    public ICommand SelectSongBookCommand { get; set; }

    public BibleSelectionViewModel(MediaService mediaService, INavigationService navigationService, IServiceScopeFactory scopeFactory)
    {
        _mediaService = mediaService;
        _navigationService = navigationService;
        _scopeFactory = scopeFactory;

        //set schedules from initial state.
        //this should fire only once 
        IDisposable subscription1 = null;
        subscription1 = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.CurrentBibleReadingSchedule != null && state.TentativeBibleReadingSchedule != null)
            {
                _current = state.CurrentBibleReadingSchedule;
                _tentative = state.TentativeBibleReadingSchedule;
                Task.Run(async () =>
                {
                    await Initialize(_tentative.LanguageCode);
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                });
                _subscriptions.Remove(subscription1);
                subscription1?.Dispose();
            }
        });

        _subscriptions.Add(subscription1);

        // Subscribe to subsequent schedule changes (skip first one)
        BibleReadingSchedule? lastCurrent = null;
        BibleReadingSchedule? lastTentative = null;
        var subscription2 = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.CurrentBibleReadingSchedule != null && state.TentativeBibleReadingSchedule != null
                && (state.CurrentBibleReadingSchedule != lastCurrent || state.TentativeBibleReadingSchedule != lastTentative))
            {
                _current = state.CurrentBibleReadingSchedule;
                _tentative = state.TentativeBibleReadingSchedule;
                lastCurrent = _current;
                lastTentative = _tentative;
            }
        });

        _subscriptions.Add(subscription2);

        BookSelectionCommand = new Command<PublicationListViewItemModel>(async x =>
        {
            IsBusy = true;
            ReduxContainer.Store.Dispatch(new BookSelectionAction
            {
                TentativeBibleReadingSchedule = new BibleReadingSchedule
                {
                    PublicationCode = x.Code,
                    LanguageCode = CurrentLanguage.Code
                }
            });
            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<BookSelectionViewModel>();
            await _navigationService.Navigate(viewModel);

            IsBusy = false;
        });

        OpenModalCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigationService.ShowModal("LanguageModal", this);
            IsBusy = false;
        });

        BackCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigationService.GoBack();
            IsBusy = false;
        });

        CloseModalCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigationService.CloseModal();
            IsBusy = false;
        });

        SelectLanguageCommand = new Command<LanguageListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentLanguage != null) CurrentLanguage.IsSelected = false;

            CurrentLanguage = x;
            CurrentLanguage.IsSelected = true;

            await _navigationService.CloseModal();
            await PopulateTranslations(x.Code);

            IsBusy = false;
        });

        _navigationService.NavigatedBack += OnNavigated;
    }

    private void OnNavigated(object viewModal)
    {
        if (viewModal.GetType() == GetType()) SetSelectedTranslation();
    }

    private void SetSelectedTranslation()
    {
        if (_current.LanguageCode == _tentative.LanguageCode)
        {
            if (SelectedTranslation != null) SelectedTranslation.IsSelected = false;

            SelectedTranslation = _translationVMsMapping[_current.PublicationCode];
            SelectedTranslation.IsSelected = true;
        }
    }

    private ObservableCollection<PublicationListViewItemModel> _translations;

    public ObservableCollection<PublicationListViewItemModel> Translations
    {
        get => _translations;
        set => SetProperty(ref _translations, value);
    }

    private ObservableCollection<LanguageListViewItemModel> _languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => _languages;
        set => SetProperty(ref _languages, value);
    }

    public PublicationListViewItemModel SelectedTranslation { get; set; }

    private LanguageListViewItemModel _currentLanguage;

    public LanguageListViewItemModel CurrentLanguage
    {
        get => _currentLanguage;
        set => SetProperty(ref _currentLanguage, value);
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string PublicationCode
    {
        get => _tentative.PublicationCode;
        set
        {
            _tentative.PublicationCode = value;
            OnPropertyChanged();
        }
    }

    private string _languageSearchTerm;

    public string LanguageSearchTerm
    {
        get => _languageSearchTerm;
        set => SetProperty(ref _languageSearchTerm, value);
    }

    public object SelectedItem => CurrentLanguage;

    private async Task Initialize(string languageCode)
    {
        await PopulateLanguages();
        await PopulateTranslations(languageCode);

        // Subscribe to LanguageSearchTerm property changes
        PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == "LanguageSearchTerm")
            {
                _ = PopulateLanguages(LanguageSearchTerm);
            }
        };
    }


    private async Task PopulateLanguages(string searchTerm = null)
    {
        var languages = await _mediaService.GetBibleLanguages();
        var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

        foreach (var language in languages.Select(x => x.Value)
                     .Where(x => searchTerm == null
                                 || x.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                     .OrderBy(x => x.Name))
        {
            var languageVm = new LanguageListViewItemModel(language);

            languageVMs.Add(languageVm);

            if (languageVm.Code == _tentative.LanguageCode)
            {
                languageVm.IsSelected = true;
                CurrentLanguage = languageVm;
            }
        }

        Languages = languageVMs;
    }

    private readonly Dictionary<string, PublicationListViewItemModel> _translationVMsMapping = [];

    private async Task PopulateTranslations(string languageCode)
    {
        _translationVMsMapping.Clear();

        var translations = await _mediaService.GetBibleTranslations(languageCode);
        var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var translation in translations.Select(x => x.Value))
        {
            var translationVm = new PublicationListViewItemModel(translation);

            translationVMs.Add(translationVm);
            _translationVMsMapping.Add(translationVm.Code, translationVm);

            if (_current.LanguageCode == languageCode
                && _current.PublicationCode == translation.Code)
            {
                translationVm.IsSelected = true;
                SelectedTranslation = translationVm;
            }
        }

        Translations = translationVMs;
    }

    public void Dispose()
    {
        _navigationService.NavigatedBack -= OnNavigated;

        _subscriptions.ForEach(x => x.Dispose());
        
        // Note: _mediaService (MediaService) is a singleton and should not be 
        // disposed here as it is managed by the DI container
    }
}