using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public class BibleSelectionViewModel : ObservableObject, IListViewModel
{
    private readonly MediaService _mediaService;
    private readonly IState<ApplicationState> _state;

    private BibleReadingSchedule _current;
    private BibleReadingSchedule _tentative;

    public ICommand BackCommand { get; set; }
    public ICommand BookSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }
    public ICommand SelectSongBookCommand { get; set; }

    public BibleSelectionViewModel(MediaService mediaService, INavigation navigation, IServiceProvider serviceProvider, IDispatcher dispatcher, IState<ApplicationState> state)
    {
        _mediaService = mediaService;
        var navigation1 = navigation;
        var serviceProvider1 = serviceProvider;
        var dispatcher1 = dispatcher;
        _state = state;

        //set schedules from initial state.
        //this should fire only once 
        EventHandler onBibleReadingInitialized = null;
        onBibleReadingInitialized = (sender, e) =>
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null) return;
            _current = stateValue.CurrentBibleReadingSchedule;
            _tentative = stateValue.TentativeBibleReadingSchedule;
            Task.Run(async () =>
            {
                await Initialize(_tentative.LanguageCode);
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });
            _state.StateChanged -= onBibleReadingInitialized;
        };
        _state.StateChanged += onBibleReadingInitialized;

        // Subscribe to subsequent schedule changes (skip first one)
        BibleReadingSchedule lastCurrent = null;
        BibleReadingSchedule lastTentative = null;

        _state.StateChanged += OnBibleReadingChanged;

        BookSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            IsBusy = true;
            dispatcher1.Dispatch(new BookSelectionAction(new BibleReadingSchedule
            {
                PublicationCode = x.Code,
                LanguageCode = CurrentLanguage.Code
            }));
            var viewModel = serviceProvider1.GetRequiredService<BookSelectionViewModel>();
            var page = serviceProvider1.GetRequiredService<BookSelection>();
            page.BindingContext = viewModel;
            await navigation1.PushAsync(page);

            IsBusy = false;
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            var modal = serviceProvider1.GetRequiredService<LanguageModal>();
            modal.BindingContext = this;
            await navigation1.PushModalAsync(modal);
            IsBusy = false;
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigation1.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            if (navigation1.ModalStack.Count > 0)
            {
                var modal = await navigation1.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
            IsBusy = false;
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentLanguage != null) CurrentLanguage.IsSelected = false;

            CurrentLanguage = x;
            CurrentLanguage.IsSelected = true;

            if (navigation1.ModalStack.Count > 0)
            {
                var modal = await navigation1.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
            await PopulateTranslations(x.Code);

            IsBusy = false;
        });

        void OnBibleReadingChanged(object sender, EventArgs e)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null || (stateValue.CurrentBibleReadingSchedule == lastCurrent && stateValue.TentativeBibleReadingSchedule == lastTentative)) return;
            _current = stateValue.CurrentBibleReadingSchedule;
            _tentative = stateValue.TentativeBibleReadingSchedule;
            lastCurrent = _current;
            lastTentative = _tentative;
        }
    }

    private void SetSelectedTranslation()
    {
        if (_current.LanguageCode != _tentative.LanguageCode) return;
        if (SelectedTranslation != null) SelectedTranslation.IsSelected = false;

        SelectedTranslation = _translationVMsMapping[_current.PublicationCode];
        SelectedTranslation.IsSelected = true;
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

            if (languageVm.Code != _tentative.LanguageCode) continue;
            languageVm.IsSelected = true;
            CurrentLanguage = languageVm;
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

            if (_current.LanguageCode != languageCode
                || _current.PublicationCode != translation.Code) continue;
            translationVm.IsSelected = true;
            SelectedTranslation = translationVm;
        }

        Translations = translationVMs;
    }
}