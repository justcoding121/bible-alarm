#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles initialization and populate logic for MusicPublicationSelectionViewModel.
/// </summary>
public sealed class MusicPublicationSelectionInitHandler
{
    private readonly IMediaService mediaService;
    private readonly MusicPublicationSelectionStateManager stateManager;
    private readonly MusicPublicationSelectionDataProvider dataProvider;
    private readonly Func<System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>> getLanguages;
    private readonly Func<LanguageListViewItemModel?> getCurrentLanguage;
    private readonly Action<LanguageListViewItemModel?> setCurrentLanguage;
    private readonly Action<Func<string?, Task>> setupLanguageSearchHandler;

    public MusicPublicationSelectionInitHandler(
        IMediaService mediaService,
        MusicPublicationSelectionStateManager stateManager,
        MusicPublicationSelectionDataProvider dataProvider,
        Func<System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>> getLanguages,
        Func<LanguageListViewItemModel?> getCurrentLanguage,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        Action<Func<string?, Task>> setupLanguageSearchHandler)
    {
        this.mediaService = mediaService;
        this.stateManager = stateManager;
        this.dataProvider = dataProvider;
        this.getLanguages = getLanguages;
        this.getCurrentLanguage = getCurrentLanguage;
        this.setCurrentLanguage = setCurrentLanguage;
        this.setupLanguageSearchHandler = setupLanguageSearchHandler;
    }

    public async Task InitializeInternalAsync(
        IState<ApplicationState> state,
        Func<string?, Task> populateLanguages,
        Func<string?, bool, IFetchProgress?, CancellationToken, Task> populateSongPublications,
        Func<string?, Task> setupLanguageSearchPopulate)
    {
        var current = stateManager.Current;
        if (current == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(current.LanguageCode))
        {
            await populateSongPublications(null, false, null, default);
            return;
        }

        var languageCode = current.LanguageCode;
        if (languageCode == null)
        {
            var languages = await mediaService.GetVocalMusicLanguages();
            languageCode = languages.ContainsKey(AppConstants.Media.DefaultLanguageCode)
                ? AppConstants.Media.DefaultLanguageCode
                : languages.FirstOrDefault().Key;
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }
            current.LanguageCode = languageCode;
        }

        var languageList = getLanguages();
        if (languageList == null || languageList.Count == 0)
        {
            await populateLanguages(null);
        }

        await populateSongPublications(languageCode, false, null, default);
        setupLanguageSearchHandler(setupLanguageSearchPopulate);
    }

    public async Task PopulateLanguagesAsync(
        IState<ApplicationState> state,
        Action<LanguageListViewItemModel?> setCurrentLanguageCallback,
        string? searchTerm = null)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var currentLanguageCode = currentSchedule?.MusicLanguageCode;
        var effectiveLanguageCode = !string.IsNullOrEmpty(currentLanguageCode) ? currentLanguageCode : AppConstants.Media.DefaultLanguageCode;
        var effectiveCurrent = new AlarmMusic { LanguageCode = effectiveLanguageCode };

        await dataProvider.PopulateLanguages(
            effectiveCurrent,
            getLanguages(),
            setCurrentLanguageCallback,
            searchTerm);
    }

    public async Task RefreshLanguagesAsync(IState<ApplicationState> state, Func<string?, Task> populateLanguages)
    {
        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var currentLanguageCode = currentSchedule?.MusicLanguageCode;
            var effectiveLanguageCode = !string.IsNullOrEmpty(currentLanguageCode)
                ? currentLanguageCode
                : AppConstants.Media.DefaultLanguageCode;
            var tempCurrent = new AlarmMusic { LanguageCode = effectiveLanguageCode };

            Serilog.Log.Debug(AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.RefreshLanguagesAsyncCurrentEffective,
                currentLanguageCode ?? "(null)", effectiveLanguageCode);

            await dataProvider.PopulateLanguages(
                tempCurrent,
                getLanguages(),
                lang => setCurrentLanguage(lang),
                null);
            setupLanguageSearchHandler(populateLanguages);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.ErrorRefreshingLanguages);
        }
    }

    public void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        var current = getCurrentLanguage();
        if (current != null)
        {
            current.IsSelected = false;
        }

        setCurrentLanguage(language);
        language.IsSelected = true;
    }
}
