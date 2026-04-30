#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Manages UI properties and property changes for bible selection.
/// </summary>
public sealed class BiblePublicationSelectionPropertyManager : ObservableObject
{
    private readonly IState<ApplicationState> state;
    private readonly BiblePublicationSelectionDataProvider dataProvider;
    private readonly BiblePublicationSelectionStateHandler stateHandler;

    private ObservableCollection<PublicationListViewItemModel>? publications;
    private ObservableCollection<LanguageListViewItemModel>? languages;
    private LanguageListViewItemModel? currentLanguage;
    private bool isBusy = true;
    private string languageSearchTerm = string.Empty;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private bool canCancelFetch = false;
    private bool hasFetchError = false;
    private bool isCancelBusy = false;

    private PropertyChangedEventHandler? propertyChangedHandler;

    public BiblePublicationSelectionPropertyManager(
        IState<ApplicationState> state,
        BiblePublicationSelectionDataProvider dataProvider,
        BiblePublicationSelectionStateHandler stateHandler)
    {
        this.state = state;
        this.dataProvider = dataProvider;
        this.stateHandler = stateHandler;
    }

    public ObservableCollection<PublicationListViewItemModel> Publications
    {
        get => publications ??= [];
        set => SetProperty(ref publications, value);
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public PublicationListViewItemModel? SelectedPublication { get; set; }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        set
        {
#if DEBUG
#endif
            SetProperty(ref isBusy, value);
        }
    }

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public bool ShowProgress
    {
        get => showProgress;
        set => SetProperty(ref showProgress, value);
    }

    public double ProgressPercent
    {
        get => progressPercent;
        set => SetProperty(ref progressPercent, value);
    }

    public string ProgressText
    {
        get => progressText;
        set => SetProperty(ref progressText, value);
    }

    public bool CanCancelFetch
    {
        get => canCancelFetch;
        set => SetProperty(ref canCancelFetch, value);
    }

    public bool HasFetchError
    {
        get => hasFetchError;
        set => SetProperty(ref hasFetchError, value);
    }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    public string PublicationCode
    {
        get => stateHandler.Current?.PublicationCode ?? "";
        set
        {
            if (stateHandler.Current == null)
            {
                return;
            }

            stateHandler.Current.PublicationCode = value;
            OnPropertyChanged();
        }
    }

    public object? SelectedItem => CurrentLanguage;

    public void UpdateCurrentLanguageFromLanguages()
    {
        if (Languages != null)
        {
            var selectedLanguages = Languages.Where(l => l.IsSelected).ToList();
            var selectedLanguage = Languages.FirstOrDefault(l => l.IsSelected);

            if (selectedLanguages.Count > 1)
            {
                Serilog.Log.Warning(AppConstants.Logging.BiblePublicationSelectionPropertyManagerDiagnosticsLog.MultipleLanguagesSelectedCount,
                    selectedLanguages.Count);
            }

            if (selectedLanguage != null)
            {
                CurrentLanguage = selectedLanguage;
            }
        }
    }

    public void SetupPropertyChangedHandler(Action<string?> populateLanguages)
    {
        propertyChangedHandler = (sender, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(LanguageSearchTerm), StringComparison.Ordinal))
            {
                populateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += propertyChangedHandler;
    }

    public void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        if (CurrentLanguage != null)
        {
            CurrentLanguage.IsSelected = false;
        }

        CurrentLanguage = language;
        CurrentLanguage!.IsSelected = true;
    }

    public void SetSelectedPublication()
    {
        // Use CurrentSchedule as the source of truth for publication code
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var publicationCode = stateValue.CurrentSchedule.BiblePublicationCode;
        
        // Clear ALL previous selections first to ensure only one publication is selected
        var mapping = dataProvider.GetPublicationVMsMapping();
        foreach (var pub in mapping.Values)
        {
            pub.IsSelected = false;
        }
        
        // Also clear the previous SelectedPublication
        if (SelectedPublication != null)
        {
            SelectedPublication.IsSelected = false;
        }

        if (string.IsNullOrEmpty(publicationCode))
        {
            SelectedPublication = null;
            return;
        }

        if (!mapping.TryGetValue(publicationCode, out var publication))
        {
            SelectedPublication = null;
            return;
        }

        SelectedPublication = publication;
        SelectedPublication!.IsSelected = true;
    }

    public void Cleanup()
    {
        if (propertyChangedHandler != null)
        {
            PropertyChanged -= propertyChangedHandler;
        }
    }
}
