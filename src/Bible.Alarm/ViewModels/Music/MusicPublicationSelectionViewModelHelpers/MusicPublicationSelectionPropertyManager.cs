#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

public sealed partial class MusicPublicationSelectionPropertyManager : ObservableObject
{
    private bool isBusy = true;
    private ObservableCollection<PublicationListViewItemModel>? songPublications;
    private ObservableCollection<LanguageListViewItemModel>? languages;
    private LanguageListViewItemModel? currentLanguage;
    private string languageSearchTerm = string.Empty;
    private PublicationListViewItemModel? selectedSongPublication;
    private PropertyChangedEventHandler? propertyChangedHandler;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private bool canCancelFetch = false;
    private bool hasFetchError = false;
    private bool isCancelBusy = false;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ObservableCollection<PublicationListViewItemModel> SongPublications
    {
        get => songPublications ??= [];
        set => SetProperty(ref songPublications, value);
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public PublicationListViewItemModel? SelectedSongPublication
    {
        get => selectedSongPublication;
        set => SetProperty(ref selectedSongPublication, value);
    }

    public object? SelectedItem => CurrentLanguage;

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

    public void UpdateCurrentLanguageFromLanguages()
    {
        if (Languages != null)
        {
            var selectedLanguage = Languages.FirstOrDefault(l => l.IsSelected);
            if (selectedLanguage != null)
            {
                CurrentLanguage = selectedLanguage;
            }
        }
    }

    public void SetupLanguageSearchHandler(Func<string?, Task> populateLanguages)
    {
        // Remove old handler if it exists to prevent duplicate handlers
        if (propertyChangedHandler != null)
        {
            PropertyChanged -= propertyChangedHandler;
        }

        propertyChangedHandler = (sender, e) =>
        {
            if (e.PropertyName == nameof(LanguageSearchTerm))
            {
                _ = populateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += propertyChangedHandler;
    }

    public void RemoveLanguageSearchHandler()
    {
        if (propertyChangedHandler != null)
        {
            PropertyChanged -= propertyChangedHandler;
            propertyChangedHandler = null;
        }
    }
}

