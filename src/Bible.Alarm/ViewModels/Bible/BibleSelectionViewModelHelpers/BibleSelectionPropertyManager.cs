#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;

namespace Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

/// <summary>
/// Manages UI properties and property changes for bible selection.
/// </summary>
public sealed class BibleSelectionPropertyManager : ObservableObject
{
    private readonly IState<ApplicationState> state;
    private readonly BibleSelectionDataProvider dataProvider;
    private readonly BibleSelectionStateHandler stateHandler;

    private ObservableCollection<PublicationListViewItemModel>? translations;
    private ObservableCollection<LanguageListViewItemModel>? languages;
    private LanguageListViewItemModel? currentLanguage;
    private bool isBusy = true;
    private string languageSearchTerm = string.Empty;

    private PropertyChangedEventHandler? propertyChangedHandler;

    public BibleSelectionPropertyManager(
        IState<ApplicationState> state,
        BibleSelectionDataProvider dataProvider,
        BibleSelectionStateHandler stateHandler)
    {
        this.state = state;
        this.dataProvider = dataProvider;
        this.stateHandler = stateHandler;
    }

    public ObservableCollection<PublicationListViewItemModel> Translations
    {
        get => translations ??= [];
        set => SetProperty(ref translations, value);
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public PublicationListViewItemModel? SelectedTranslation { get; set; }

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
                Serilog.Log.Warning("[BibleSelectionPropertyManager] Multiple languages selected: {Languages}",
                    string.Join(", ", selectedLanguages.Select(l => $"{l.Name} ({l.Code})")));
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
            if (e.PropertyName == "LanguageSearchTerm")
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

    public void SetSelectedTranslation()
    {
        // Use CurrentSchedule as the source of truth for publication code
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var publicationCode = stateValue.CurrentSchedule.BiblePublicationPublicationCode;
        if (string.IsNullOrEmpty(publicationCode))
        {
            return;
        }

        if (SelectedTranslation != null)
        {
            SelectedTranslation.IsSelected = false;
        }

        if (!dataProvider.GetTranslationVMsMapping().TryGetValue(publicationCode, out var translation))
        {
            return;
        }

        SelectedTranslation = translation;
        SelectedTranslation!.IsSelected = true;
    }

    public void Cleanup()
    {
        if (propertyChangedHandler != null)
        {
            PropertyChanged -= propertyChangedHandler;
        }
    }
}
