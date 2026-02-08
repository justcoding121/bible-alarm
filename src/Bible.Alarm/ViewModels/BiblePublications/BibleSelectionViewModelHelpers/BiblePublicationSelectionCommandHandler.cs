#nullable enable
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for bible selection operations.
/// </summary>
public sealed class BiblePublicationSelectionCommandHandler
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    public BiblePublicationSelectionCommandHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper,
        IBiblePublicationService? biblePublicationService = null,
        ILanguageContentService? languageContentService = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;
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
            Log.Debug("CreateSectionSelectionCommand: Starting for publication={PublicationCode}, biblePublicationService={HasService}",
                x?.Code ?? "(null)", biblePublicationService != null);

            if (x == null)
            {
                Log.Warning("CreateSectionSelectionCommand: Publication is null, returning");
                return;
            }

            // Always use CurrentSchedule as the source of truth for language
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                Log.Warning("CreateSectionSelectionCommand: CurrentSchedule is null, returning");
                return;
            }

            // For item-click fetches, we show per-row progress (spinner + percent) instead of modal overlays.
            // The caller sets IsNavigating; progress will be set by FetchProgressTracker only when a fetch actually happens.

            // No DB probing: the tapped publication row already knows whether it has LanguageId or not.
            // If it's a publication without language FK, use "E" (English default) for cascade consistency.
            // This ensures the schedule's language is consistent with what the publication modal will show.
            // Otherwise, prefer the currently-selected language in the UI, then fall back to schedule language.
            var languageCode = x.IsPublicationWithoutLanguage
                ? "E"
                : (currentSchedule.BiblePublicationLanguageCode ??
                   getCurrentLanguage()?.Code ??
                   x.PublicationLanguageCode ??
                   "E");

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
                    Name = string.Empty
                });
            }
            else
            {
                var languages = await mediaService.GetBiblePublicationLanguages();
                if (languages.TryGetValue(languageCode, out var language))
                {
                    currentLanguage = new LanguageListViewItemModel(language);
                }
                else
                {
                    // Create a minimal language item from the code if not found in collection
                    currentLanguage = new LanguageListViewItemModel(new Language
                    {
                        Id = 0,
                        LanguageCode = languageCode,
                        Name = languageCode
                    });
                }
            }

            Log.Debug("CreateSectionSelectionCommand: Calling GetSectionAndTrackForPublicationAsync for publication={PublicationCode}, language={LanguageCode}",
                x.Code, currentLanguage.Code);
            
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);
            
            // Create progress tracker for per-row percent updates (no modal progress card / busy overlay).
            var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                progress => _ = MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = progress),
                _ => { },
                _ => { });
            
            (string? sectionCode, string trackCode, string sectionName, string trackTitle) result;
            try
            {
                result = await itemSelector.GetSectionAndTrackForPublicationAsync(x, currentLanguage, progressTracker);
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
            {
                // List item click failure: show toast and close modal (retain state)
                Log.Warning(ex, "CreateSectionSelectionCommand: Network error for publication={PublicationCode}", x.Code);
                // Reset progress on error (only if it was set during fetch)
                await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                var toastService = ServiceProviderManager.GetService<IToastService>();
                await toastService.ShowMessage("Unable to load. Please check your connection.");
                await navigationService.PopModalAsync();
                return;
            }
            var (sectionCode, trackCode, sectionName, trackTitle) = result;

            Log.Debug("CreateSectionSelectionCommand: Result sectionCode={SectionCode}, trackCode={TrackCode}, sectionName={SectionName}, trackTitle={TrackTitle}",
                sectionCode, trackCode, sectionName, trackTitle);

            // trackCode must be valid; sectionCode can be null/empty for non-sectioned publications (dramas)
            if (string.IsNullOrWhiteSpace(trackCode))
            {
                Log.Warning("CreateSectionSelectionCommand: Invalid trackCode={TrackCode}, returning", trackCode ?? "(null)");
                return;
            }

            // Warn if names are empty - this could cause empty rows in the UI
            if (!string.IsNullOrWhiteSpace(sectionCode) && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning("CreateSectionSelectionCommand: SectionName is empty for sectionCode={SectionCode}, publication={PublicationCode}. This may cause empty section row in UI.",
                    sectionCode, x.Code);
            }
            if (string.IsNullOrWhiteSpace(trackTitle))
            {
                Log.Warning("CreateSectionSelectionCommand: TrackTitle is empty for trackCode={TrackCode}, publication={PublicationCode}. This may cause empty track row in UI.",
                    trackCode, x.Code);
            }

            var biblePublicationItem = CreateBiblePublicationItemFromSelection(x, sectionCode, trackCode, sectionName, trackTitle, currentLanguage, currentSchedule);

            Log.Information("CreateSectionSelectionCommand: Dispatching selection for publication={PublicationCode}, section={SectionCode}, track={TrackCode}, sectionName={SectionName}, trackTitle={TrackTitle}",
                x.Code, sectionCode ?? "(null)", trackCode, sectionName, trackTitle);

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

            // Track if progress was set (fetch happened) - only show completion if fetch occurred
            bool fetchOccurred = false;
            try
            {
                var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
                var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
                var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);
                
                // Create progress tracker - updates only the list item progress
                var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                    progress => _ = MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        x.DownloadProgress = progress;
                        fetchOccurred = true; // Mark that fetch occurred when progress is set
                    }),
                    text => { }, // No text updates for list item progress
                    isVisible => { }); // No visibility updates for list item progress
                
                (string? publicationCode, string? sectionCode, string trackCode, string sectionName, string publicationName, string trackTitle) result;
                try
                {
                    result = await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(x, progressTracker);
                }
                catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
                {
                    // List item click failure: show toast and close modal (retain state)
                    Log.Warning(ex, "BibleSelectionCommandHandler: Network error during language selection for {LanguageCode}", x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        x.DownloadProgress = 0.0;
                    });
                    var toastService = ServiceProviderManager.GetService<IToastService>();
                    await toastService.ShowMessage("Unable to load. Please check your connection.");
                    await navigationService.PopModalAsync();
                    return;
                }
                var (publicationCode, sectionCode, trackCode, sectionName, publicationName, trackTitle) = result;

                // Check for both null and empty string - GetPublicationSectionAndTrackForLanguageAsync returns empty string on failure
                if (string.IsNullOrEmpty(publicationCode))
                {
                    Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - No publications found for language {LanguageCode}", x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    return;
                }

                // Validate that we have valid track code (sectionCode can be null for non-sectioned publications like dramas)
                if (string.IsNullOrWhiteSpace(trackCode))
                {
                    Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - Invalid track ({TrackCode}) for language {LanguageCode}", 
                        trackCode ?? "(null)", x.Code);
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    return;
                }

                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule == null)
                {
                    Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - CurrentSchedule is null");
                    await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                    return;
                }

                Log.Information("BibleSelectionCommandHandler: SelectLanguageCommand - Creating item for language {LanguageCode}, publication {PublicationCode}, section {SectionCode}, track {TrackCode}",
                    x.Code, publicationCode, sectionCode, trackCode);

                // Only update the UI selection after we know we have valid content.
                // If fetching/harvesting fails, we must keep the previous language selection (and schedule state) unchanged.
                updateSelectedLanguage(x);

                var biblePublicationItem = CreateBiblePublicationItemForLanguageSelection(
                    x, publicationCode, sectionCode, trackCode, sectionName, publicationName, trackTitle, currentSchedule);
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

    private BiblePublicationStateItem CreateBiblePublicationItemFromSelection(
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
            Log.Error("CreateBiblePublicationItemFromSelection: Category is null in current schedule. This is a bug - category must always be selected. Publication={PublicationCode}, ScheduleId={ScheduleId}",
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

    private BiblePublicationStateItem CreateBiblePublicationItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        string sectionName,
        string publicationName,
        string trackTitle,
        ScheduleStateItem currentSchedule)
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
