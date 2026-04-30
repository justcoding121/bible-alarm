#nullable enable
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for bible selection operations.
/// </summary>
public sealed class BiblePublicationSelectionCommandHandler
{
    private const string LogNullPlaceholder = "(null)";

    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;

    public BiblePublicationSelectionCommandHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IBiblePublicationService? biblePublicationService = null,
        ILanguageContentService? languageContentService = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
    }

    public ICommand CreateSectionSelectionCommand(
        Func<LanguageListViewItemModel?> getCurrentLanguage,
        Func<ObservableCollection<PublicationListViewItemModel>> getPublications,
        Func<Dictionary<string, PublicationListViewItemModel>> getPublicationVMsMapping,
        Func<BiblePublicationSchedule?> getCurrent,
        Action<bool> setShowProgress,
        Action<double> setProgressPercent,
        Action<string> setProgressText,
        Action<bool> setIsBusy)
    {
        return new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionStarting,
                x?.Code ?? LogNullPlaceholder, biblePublicationService != null);

            if (x == null)
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionPublicationNullReturning);
                return;
            }

            // Always use CurrentSchedule as the source of truth for language
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionCurrentScheduleNullReturning);
                return;
            }

            // For item-click fetches, we show per-row progress (spinner + percent) instead of modal overlays.
            // The caller sets IsNavigating; progress will be set by ModalOverlayFetchProgressReporter only when a fetch actually happens.

            // No DB probing: the tapped publication row already knows whether it has LanguageId or not.
            // If it's a publication without language FK (e.g. iam), use the current schedule's language
            // so the language row stays at the user's choice (e.g. MY) after the modal closes.
            // Otherwise, prefer the currently-selected language in the UI, then fall back to schedule language.
            var languageCode = x.IsPublicationWithoutLanguage
                ? (currentSchedule.BiblePublicationLanguageCode ?? AppConstants.Media.DefaultLanguageCode)
                : (currentSchedule.BiblePublicationLanguageCode ??
                   getCurrentLanguage()?.Code ??
                   x.PublicationLanguageCode ??
                   AppConstants.Media.DefaultLanguageCode);

            // Get language from the languages collection
            // If languageCode is empty/null, create a minimal language item (for publications without language)
            LanguageListViewItemModel currentLanguage;
            if (string.IsNullOrEmpty(languageCode))
            {
                // Publication doesn't have a language - create a minimal language item with empty code
                // The itemSelector will handle this correctly
                currentLanguage = new LanguageListViewItemModel(new Language
                {
                    Id = 0,
                    LanguageCode = string.Empty,
                    Direction = AppConstants.Media.TextDirectionLeftToRight
                }, string.Empty);
            }
            else
            {
                var languageNameService = ServiceProviderManager.GetService<ILanguageNameService>();
                var languages = await mediaService.GetBiblePublicationLanguages();
                if (languages.TryGetValue(languageCode, out var language))
                {
                    var name = languageNameService != null
                        ? await languageNameService.GetNameAsync(language.Id, AppConstants.Media.DefaultLanguageCode) ?? languageCode
                        : languageCode;
                    currentLanguage = new LanguageListViewItemModel(language, name);
                }
                else
                {
                    // Create a minimal language item from the code if not found in collection
                    currentLanguage = new LanguageListViewItemModel(new Language
                    {
                        Id = 0,
                        LanguageCode = languageCode,
                        Direction = AppConstants.Media.TextDirectionLeftToRight
                    }, languageCode);
                }
            }

            Log.Debug(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionCallingGetSectionAndTrack,
                x.Code, currentLanguage.Code);

            // Do NOT check internet upfront - English publications are pre-packaged; others may be cached.
            // If a fetch is needed and network is down, the selector will throw and we catch below.

            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);
            
            var progressReporter = new Bible.Alarm.Common.Helpers.ListItemFetchProgressReporter("BiblePublication", x.Code);

            (string? sectionCode, string trackCode, string sectionName, string trackTitle) result;
            try
            {
                    result = await itemSelector.GetSectionAndTrackForPublicationAsync(x, currentLanguage, progressReporter);
            }
                catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
                {
                    Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionNetworkErrorForPublication, x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    var toastService = ServiceProviderManager.GetService<IToastService>();
                    await Task.Delay(500);
                    await navigationService.PopModalAsync();
                    await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
                    return;
                }
            var (sectionCode, trackCode, sectionName, trackTitle) = result;

            Log.Debug(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionResult,
                sectionCode, trackCode, sectionName, trackTitle);

            // trackCode must be valid; sectionCode can be null/empty for non-sectioned publications (dramas)
            if (string.IsNullOrWhiteSpace(trackCode))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionInvalidTrackCodeReturning, trackCode ?? LogNullPlaceholder);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("This content is not available at the moment"));
                return;
            }

            // Warn if names are empty - this could cause empty rows in the UI
            if (!string.IsNullOrWhiteSpace(sectionCode) && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionSectionNameEmptyMayCauseEmptySectionRow,
                    sectionCode, x.Code);
            }
            if (string.IsNullOrWhiteSpace(trackTitle))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionTrackTitleEmptyMayCauseEmptyTrackRow,
                    trackCode, x.Code);
            }

            var biblePublicationItem = CreateBiblePublicationItemFromSelection(x, sectionCode, trackCode, sectionName, trackTitle, currentLanguage, currentSchedule);

            Log.Information(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionDispatchingSelection,
                x.Code, sectionCode ?? LogNullPlaceholder, trackCode, sectionName, trackTitle);

            var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchBiblePublicationSelectionActions(biblePublicationItem);
            
            // Wait for cascade to complete by checking state matches the dispatched values
            // Check for the SPECIFIC publication code we just dispatched (not just "non-empty")
            // to avoid exiting early when stale values from a previous selection are still present.
            const int maxWaitAttempts = 30;
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                var currentState = state.Value.CurrentSchedule;
                if (currentState != null && 
                    currentState.BiblePublicationCode == x.Code &&
                    !string.IsNullOrWhiteSpace(currentState.BiblePublicationTrackCode))
                {
                    break;
                }
                await Task.Delay(delayMs);
            }
            
            await navigationService.PopModalAsync();
        });
    }

    public ICommand CreateBackCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });
    }

    public ICommand CreateCloseModalCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
    }

    public ICommand CreateSelectLanguageCommand(
        Func<ObservableCollection<LanguageListViewItemModel>> getLanguages,
        Func<Dictionary<string, PublicationListViewItemModel>> getPublicationVMsMapping,
        Action<LanguageListViewItemModel> updateSelectedLanguage,
        Action<bool> setShowProgress,
        Action<double> setProgressPercent,
        Action<string> setProgressText,
        Action<bool> setIsBusy)
    {
        return new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            // Do NOT check internet upfront - English is pre-packaged and needs no fetch.
            // If a fetch is needed and network is down, the selector will throw and we catch below.

            // Track if progress was set (fetch happened) - only show completion if fetch occurred
            bool fetchOccurred = false;
            try
            {
                var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
                var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
                var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);

                var progressReporter = new Bible.Alarm.Common.Helpers.ListItemFetchProgressReporter(
                    "BibleLanguage", x.Code, () => fetchOccurred = true);

                (string? publicationCode, string? sectionCode, string trackCode, string sectionName, string publicationName, string trackTitle) result;
                try
                {
                    result = await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(x, progressReporter);
                }
                catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
                {
                    Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageNetworkErrorDuringSelection, x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    var toastService = ServiceProviderManager.GetService<IToastService>();
                    await Task.Delay(500);
                    await navigationService.PopModalAsync();
                    await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
                    return;
                }
                var (publicationCode, sectionCode, trackCode, sectionName, publicationName, trackTitle) = result;

                // Check for both null and empty string - GetPublicationSectionAndTrackForLanguageAsync returns empty string on failure
                if (string.IsNullOrEmpty(publicationCode))
                {
                    Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageNoPublicationsFoundForLanguage, x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    return;
                }

                // Validate that we have valid track code (sectionCode can be null for non-sectioned publications like dramas)
                if (string.IsNullOrWhiteSpace(trackCode))
                {
                    Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageInvalidTrackForLanguage, 
                        trackCode ?? LogNullPlaceholder, x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    return;
                }

                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule == null)
                {
                    Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageCurrentScheduleNull);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    return;
                }

                Log.Information(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageCreatingItem,
                    x.Code, publicationCode, sectionCode, trackCode);

                // Only update the UI selection after we know we have valid content.
                // If fetching/cataloging fails, we must keep the previous language selection (and schedule state) unchanged.
                updateSelectedLanguage(x);

                var biblePublicationItem = CreateBiblePublicationItemForLanguageSelection(
                    x, publicationCode, sectionCode, trackCode, sectionName, publicationName, trackTitle);
                var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
                actionDispatcher.DispatchLanguageSelectionActions(biblePublicationItem);
                
                // Wait for cascade to complete - check for the SPECIFIC language we just dispatched
                const int maxWaitAttempts = 30;
                const int delayMs = 200;
                for (int i = 0; i < maxWaitAttempts; i++)
                {
                    var currentState = state.Value.CurrentSchedule;
                    if (currentState != null && 
                        currentState.BiblePublicationLanguageCode == x.Code &&
                        !string.IsNullOrEmpty(currentState.BiblePublicationCode) &&
                        !string.IsNullOrWhiteSpace(currentState.BiblePublicationTrackCode))
                    {
                        break;
                    }
                    await Task.Delay(delayMs);
                }
            }
            finally
            {
                // Only set to 1.0 if a fetch actually occurred (progress was set during operation)
                if (fetchOccurred)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        x.DownloadProgress = 1.0;
                    });
                }
                else
                {
                    // No fetch occurred - reset progress to not-set state
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        x.DownloadProgress = -1.0;
                    });
                }
            }
            
            await navigationService.PopModalAsync();
        });
    }

    private static BiblePublicationStateItem CreateBiblePublicationItemFromSelection(
        PublicationListViewItemModel publication,
        string? sectionCode,
        string trackCode,
        string sectionName,
        string trackTitle,
        LanguageListViewItemModel language,
        ScheduleStateItem currentSchedule)
    {
        // Match the pattern used in BiblePublicationSectionSelectionViewModel and TrackSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
        // Category should NEVER be null in current schedule - if it is, that's a bug that needs to be fixed at the source
        var categoryId = currentSchedule.BiblePublicationCategoryId;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            // Category is null in current schedule - this should NEVER happen
            // Category can only be changed via CategorySelectionAction and should always be preserved
            Log.Error(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateBiblePublicationItemCategoryNullBug,
                publication.Code, currentSchedule.Id);
        }
        
        return new BiblePublicationStateItem
        {
            CategoryId = categoryId,
            CategoryName = categoryName,
            PublicationCode = publication.Code,
            LanguageCode = language.Code,
            SectionCode = sectionCode,
            TrackCode = trackCode,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publication.Name,
            SectionName = sectionName,
            TrackTitle = trackTitle
        };
    }

    private static BiblePublicationStateItem CreateBiblePublicationItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        string sectionName,
        string publicationName,
        string trackTitle)
    {
        // Match the pattern used in BiblePublicationSectionSelectionViewModel and TrackSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        return new BiblePublicationStateItem
        {
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            SectionCode = sectionCode,
            TrackCode = trackCode,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publicationName,
            SectionName = sectionName,
            TrackTitle = trackTitle
        };
    }
}
