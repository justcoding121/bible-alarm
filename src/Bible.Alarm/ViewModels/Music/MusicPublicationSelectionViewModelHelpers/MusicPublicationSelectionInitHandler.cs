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
    private readonly MusicPublicationSelectionPropertyManager propertyManager;

    public MusicPublicationSelectionInitHandler(
        IMediaService mediaService,
        MusicPublicationSelectionStateManager stateManager,
        MusicPublicationSelectionDataProvider dataProvider,
        MusicPublicationSelectionPropertyManager propertyManager)
    {
        this.mediaService = mediaService;
        this.stateManager = stateManager;
        this.dataProvider = dataProvider;
        this.propertyManager = propertyManager;
    }

    public async Task InitializeInternalAsync(
        IState<ApplicationState> state,
        Func<string?, Task> populateLanguages,
        Func<string?, bool, IFetchProgress?, CancellationToken, Task> populateSongPublications,
        Func<string?, Task> setupLanguageSearchHandler)
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

        if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
        {
            await populateLanguages(null);
        }

        await populateSongPublications(languageCode, false, null, default);
        propertyManager.SetupLanguageSearchHandler(setupLanguageSearchHandler);
    }

    public async Task PopulateLanguagesAsync(
        IState<ApplicationState> state,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        string? searchTerm = null)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var currentLanguageCode = currentSchedule?.MusicLanguageCode;
        var effectiveLanguageCode = !string.IsNullOrEmpty(currentLanguageCode) ? currentLanguageCode : AppConstants.Media.DefaultLanguageCode;
        var effectiveCurrent = new AlarmMusic { LanguageCode = effectiveLanguageCode };

        await dataProvider.PopulateLanguages(
            effectiveCurrent,
            propertyManager.Languages,
            setCurrentLanguage,
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
                propertyManager.Languages,
                lang => propertyManager.CurrentLanguage = lang,
                null);
            propertyManager.SetupLanguageSearchHandler(populateLanguages);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.ErrorRefreshingLanguages);
        }
    }

    public void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        if (propertyManager.CurrentLanguage != null)
        {
            propertyManager.CurrentLanguage.IsSelected = false;
        }

        propertyManager.CurrentLanguage = language;
        propertyManager.CurrentLanguage!.IsSelected = true;
    }
}
