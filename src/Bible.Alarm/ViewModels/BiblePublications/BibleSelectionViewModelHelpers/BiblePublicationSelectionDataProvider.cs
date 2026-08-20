#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles data population for bible selection (languages and publications).
/// </summary>
public sealed class BiblePublicationSelectionDataProvider
{
    private readonly IMediaService mediaService;
    private readonly ILanguageNameService languageNameService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly SemaphoreSlim languagePopulationLock = new(1, 1);

    public readonly Dictionary<string, PublicationListViewItemModel> publicationVMsMapping = [];

    public BiblePublicationSelectionDataProvider(
        IMediaService mediaService,
        ILanguageNameService languageNameService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.mediaService = mediaService;
        this.languageNameService = languageNameService;
        this.state = state;
        this.dispatcher = dispatcher;
    }

    public void ClearPublicationVMsMapping()
    {
        publicationVMsMapping.Clear();
    }

    public async Task PopulateLanguagesAsync(
        string? searchTerm,
        ObservableCollection<LanguageListViewItemModel>? languages)
    {
        if (languages == null) return;

        // Prevent concurrent population which can cause duplicates
        await ConcurrencyHelper.ExecuteAsync(languagePopulationLock, async () =>
        {
            // Capture current language code and category before Task.Run to avoid state access issues
            var currentLanguageCode = state.Value.CurrentSchedule?.BiblePublicationLanguageCode;
            var currentCategoryName = state.Value.CurrentSchedule?.BiblePublicationCategoryName;

            // Languages ARE filtered by category - show only languages that have publications in the selected category.
            var languagesData = await mediaService.GetBiblePublicationLanguages(currentCategoryName);
            var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

            var languageIds = languagesData.Values.Select(l => l.Id).ToList();
            var names = await languageNameService.GetNamesAsync(languageIds, AppConstants.Media.DefaultLanguageCode);

            Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulateLanguagesLoaded,
                languagesData.Count, currentLanguageCode ?? "(null)");

            var languageVMs = BuildSortedLanguageViewModels(languagesData, names, trimmedSearchTerm, currentLanguageCode);

            // Add items in small batches with frequent yields for smooth spinner animation
            const int batchSize = 15;
            await MainThread.InvokeOnMainThreadAsync(() => languages.Clear());
            // Let spinner animate after clear
            await Task.Yield();

            for (int i = 0; i < languageVMs.Count; i += batchSize)
            {
                var batch = languageVMs.Skip(i).Take(batchSize).ToList();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var lang in batch)
                    {
                        languages.Add(lang);
                    }
                });

                // Yield after every batch for smooth animation
                await Task.Yield();
            }
        });
    }

    private static List<LanguageListViewItemModel> BuildSortedLanguageViewModels(
        Dictionary<string, Language> languagesData,
        Dictionary<int, string> names,
        string? trimmedSearchTerm,
        string? currentLanguageCode)
    {
        var languageVMs = new List<LanguageListViewItemModel>();

        foreach (var language in languagesData.Values)
        {
            var name = names.GetValueOrDefault(language.Id) ?? language.LanguageCode;
            if (trimmedSearchTerm != null && !name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var languageVm = new LanguageListViewItemModel(language, name);

            if (!string.IsNullOrEmpty(currentLanguageCode) &&
                string.Equals(languageVm.Code, currentLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                languageVm.IsSelected = true;
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulateLanguagesMarkedSelected,
                    languageVm.Code, languageVm.Name);
            }

            languageVMs.Add(languageVm);
        }

        return languageVMs.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // Using centralized sorting helper from Bible.Alarm.Shared.Helpers.PublicationSortHelper

    public async Task PopulatePublicationsAsync(
        string languageCode,
        ObservableCollection<PublicationListViewItemModel>? publications,
        bool languageChanged,
        bool downloadAll = false,
        string? categoryName = null,
        IFetchProgress? progress = null)
    {
        if (publications == null) return;

        // Capture state values before Task.Run to avoid state access issues
        var stateValue = state.Value;
        var currentPublicationCode = stateValue.CurrentSchedule?.BiblePublicationCode;
        var currentLanguageName = stateValue.CurrentSchedule?.BiblePublicationLanguageName;
        var currentLanguageDirection = stateValue.CurrentSchedule?.BiblePublicationLanguageDirection;
        // Use provided categoryName if available, otherwise fall back to state
        var currentCategoryName = categoryName ?? stateValue.CurrentSchedule?.BiblePublicationCategoryName;

        // Ensure we always have a valid category - never null or "all"
        // A category should always be selected - if it's null, this is an error condition
        if (string.IsNullOrWhiteSpace(currentCategoryName))
        {
            throw new InvalidOperationException(
                $"PopulatePublicationsAsync: Category is null or empty. Category must always be selected. LanguageCode={languageCode}, PublicationCode={currentPublicationCode}");
        }

        var (publicationVMs, newMapping, defaultPublication) =
            await Task.Run(() => BuildPublicationPopulateResultAsync(languageCode, currentCategoryName, downloadAll, progress));

        publicationVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            publicationVMsMapping[kvp.Key] = kvp.Value;
        }

        // IMPORTANT: Only dispatch default publication if user hasn't already selected a publication
        // This prevents overwriting user's selection (e.g., when switching from "melodies" to "original songs")
        MaybeDispatchDefaultPublicationAfterLanguageChange(
            languageChanged,
            defaultPublication,
            languageCode,
            currentLanguageName,
            currentLanguageDirection);

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            publications.Clear();
            foreach (var trans in publicationVMs)
            {
                publications.Add(trans);
            }
        });
    }

    private void MaybeDispatchDefaultPublicationAfterLanguageChange(
        bool languageChanged,
        PublicationListViewItemModel? defaultPublication,
        string languageCode,
        string? currentLanguageName,
        string? currentLanguageDirection)
    {
        if (!languageChanged || defaultPublication == null) return;

        var currentSchedule = state.Value.CurrentSchedule;
        if (UserHasPublicationSelectedForLanguage(currentSchedule, languageCode)) return;

        if (ScheduleAlreadyReflectsDefaultPublicationStart(currentSchedule, languageCode, defaultPublication)) return;

        _ = DispatchDefaultPublicationAsync(languageCode, defaultPublication, currentLanguageName, currentLanguageDirection);
    }

    private static bool UserHasPublicationSelectedForLanguage(ScheduleStateItem? schedule, string languageCode) =>
        schedule != null &&
        !string.IsNullOrWhiteSpace(schedule.BiblePublicationCode) &&
        schedule.BiblePublicationLanguageCode == languageCode;

    private static bool ScheduleAlreadyReflectsDefaultPublicationStart(
        ScheduleStateItem? schedule,
        string languageCode,
        PublicationListViewItemModel defaultPublication)
    {
        if (schedule == null) return false;

        var sectionCode = schedule.BiblePublicationSectionCode;
        return schedule.BiblePublicationLanguageCode == languageCode &&
               schedule.BiblePublicationCode == defaultPublication.Code &&
               string.Equals(sectionCode, "1", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(schedule.BiblePublicationTrackCode, "1", StringComparison.Ordinal);
    }

    private async Task<(List<PublicationListViewItemModel> Vms, Dictionary<string, PublicationListViewItemModel> Mapping, PublicationListViewItemModel?
        PreferredDefault)> BuildPublicationPopulateResultAsync(
        string languageCode,
        string currentCategoryName,
        bool downloadAll,
        IFetchProgress? progress)
    {
        var publicationsData =
            await LoadPublicationsDictionaryForPopulateAsync(languageCode, currentCategoryName, downloadAll, progress);

        var vms = new List<PublicationListViewItemModel>();
        var mapping = new Dictionary<string, PublicationListViewItemModel>(StringComparer.OrdinalIgnoreCase);

        if (publicationsData == null)
        {
            return (vms, mapping, null);
        }

        RemoveIncompleteCatalogPlaceholders(publicationsData);

        foreach (var publication in publicationsData.Values)
        {
            if (mapping.TryGetValue(publication.PublicationCode, out _))
            {
                continue;
            }

            var publicationVm = new PublicationListViewItemModel(publication);
            vms.Add(publicationVm);
            mapping[publicationVm.Code] = publicationVm;
        }

        vms = PublicationSortHelper.SortByPriorityForCategory(vms, p => p.Code, p => p.Name, currentCategoryName).ToList();

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsSortedFirstThreeCodes,
            vms.Count, currentCategoryName,
            string.Join(", ", vms.Take(3).Select(p => p.Code)));

        var preferredDefault = vms.Count > 0 ? vms[0] : null;

        return (vms, mapping, preferredDefault);
    }

    private static void RemoveIncompleteCatalogPlaceholders(Dictionary<string, BiblePublication> publicationsData)
    {
        var unfetchableCodes = publicationsData
            .Where(kvp => !IsPublicationFullyCataloged(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        if (unfetchableCodes.Count == 0)
        {
            return;
        }

        foreach (var code in unfetchableCodes)
        {
            publicationsData.Remove(code);
        }

        Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsRemovedUnfetchablePlaceholders,
            unfetchableCodes.Count, string.Join(", ", unfetchableCodes));
    }

    private async Task<Dictionary<string, BiblePublication>?>
        LoadPublicationsDictionaryForPopulateAsync(
            string languageCode,
            string currentCategoryName,
            bool downloadAll,
            IFetchProgress? progress)
    {
        if (!downloadAll)
        {
            return await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll, progress);
        }

        var initialPublications = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, null);
        var expectedPublicationCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, currentCategoryName);
        var actualPublicationCount = initialPublications?.Values.Count ?? 0;
        var hasAllExpected = actualPublicationCount >= expectedPublicationCount;
        var allPublicationsCataloged = hasAllExpected && initialPublications != null && initialPublications.Values.Count > 0 &&
            initialPublications.Values.All(IsPublicationFullyCataloged);

        if (allPublicationsCataloged)
        {
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedAlreadyCatalogedSkippingFetch,
                expectedPublicationCount, languageCode, currentCategoryName);
            return initialPublications;
        }

        var cancellationToken = progress?.CancellationToken ?? CancellationToken.None;
        return await RetryFetchBiblePublicationsUntilCatalogedAsync(languageCode, currentCategoryName, downloadAll,
            progress, cancellationToken);
    }

    private async Task<Dictionary<string, BiblePublication>?>
        RetryFetchBiblePublicationsUntilCatalogedAsync(
            string languageCode,
            string currentCategoryName,
            bool downloadAll,
            IFetchProgress? progress,
            CancellationToken cancellationToken)
    {
        const int maxRetries = 10;
        var retryDelay = 1000;
        var maxWaitTime = TimeSpan.FromSeconds(60);
        var startTime = DateTime.UtcNow;
        var allCataloged = false;
        var attempt = 0;
        var previousCatalogedCount = -1;

        Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsStartingFetchWithRetries,
            languageCode, currentCategoryName);

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        Dictionary<string, BiblePublication>? publicationsData = null;

        try
        {
            while (!allCataloged && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                attempt++;

                try
                {
                    var outcome =
                        await RunBiblePublicationRetryIterationAsync(new BiblePublicationRetryIterationInput
                        {
                            LanguageCode = languageCode,
                            CurrentCategoryName = currentCategoryName,
                            DownloadAll = downloadAll,
                            Progress = progress,
                            CancellationToken = cancellationToken,
                            Attempt = attempt,
                            RetryDelayBase = retryDelay,
                            PreviousCatalogedCount = previousCatalogedCount
                        });

                    publicationsData = outcome.NextSnapshot;
                    if (outcome.Completed)
                    {
                        allCataloged = true;
                        if (outcome.LogSuccess)
                        {
                            Log.Information(
                                AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedCatalogedOnAttempt,
                                outcome.ExpectedCountForLog, attempt, languageCode, currentCategoryName);
                        }
                    }
                    else if (outcome.BreakRetries)
                    {
                        break;
                    }
                    else
                    {
                        previousCatalogedCount = outcome.UpdatedCatalogedCount;
                    }
                }
                catch (Exception ex)
                {
                    if (ShouldRethrowPublicationRetryExceptionImmediately(ex))
                    {
                        throw;
                    }

                    Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptFailedWillRetry,
                        attempt, languageCode);

                    var delay = Math.Min(retryDelay * attempt, 5000);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        finally
        {
            progress?.SetIsVisible(false);
        }

        if (!allCataloged)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsTimeoutWaitingForCatalog,
                attempt, languageCode);

            publicationsData = await TryFetchPublicationListWhenSnapshotEmptyAsync(
                languageCode, currentCategoryName, publicationsData, progress);
        }

        return publicationsData;
    }

    private static bool ShouldRethrowPublicationRetryExceptionImmediately(Exception ex) =>
        ex is OperationCanceledException or HttpRequestException or SocketException ||
        NetworkExceptionHelper.IsNetworkFailure(ex);

    private async Task<Dictionary<string, BiblePublication>?> TryFetchPublicationListWhenSnapshotEmptyAsync(
        string languageCode,
        string currentCategoryName,
        Dictionary<string, BiblePublication>? publicationsData,
        IFetchProgress? progress)
    {
        if (publicationsData != null && publicationsData.Count > 0)
        {
            return publicationsData;
        }

        try
        {
            return await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, progress);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsFinalFetchAttemptFailed,
                languageCode);
            return publicationsData;
        }
    }

    private async Task<BiblePublicationRetryIterationOutcome> RunBiblePublicationRetryIterationAsync(
        BiblePublicationRetryIterationInput input)
    {
        var languageCode = input.LanguageCode;
        var currentCategoryName = input.CurrentCategoryName;
        var attempt = input.Attempt;
        var previousCatalogedCount = input.PreviousCatalogedCount;

        var fetchedWithProgress =
            await mediaService.GetBiblePublications(languageCode, currentCategoryName, input.DownloadAll, input.Progress);

        await Task.Delay(500, input.CancellationToken);

        var reQueriedData =
            await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, null);

        var retryExpectedCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, currentCategoryName);
        var retryActualCount = reQueriedData?.Values.Count ?? 0;
        var retryHasAllExpected = retryActualCount >= retryExpectedCount;
        var retryAllCataloged = retryHasAllExpected && reQueriedData != null && reQueriedData.Values.Count > 0 &&
            reQueriedData.Values.All(IsPublicationFullyCataloged);

        if (retryAllCataloged)
        {
            return BiblePublicationRetryIterationOutcome.ForSuccess(reQueriedData!, retryExpectedCount);
        }

        var currentCatalogedCount = reQueriedData?.Values.Count(IsPublicationFullyCataloged) ?? 0;

        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
        {
            Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsNoProgressBetweenRetriesStopping,
                currentCatalogedCount, retryExpectedCount, languageCode, currentCategoryName);
            return BiblePublicationRetryIterationOutcome.ForStagnation(reQueriedData);
        }

        LogBiblePublicationRetryAttemptDiagnostics(attempt, reQueriedData, retryHasAllExpected, retryActualCount,
            retryExpectedCount);

        var delay = Math.Min(input.RetryDelayBase * attempt, 5000);
        await Task.Delay(delay, input.CancellationToken);

        return BiblePublicationRetryIterationOutcome.ForContinue(fetchedWithProgress, currentCatalogedCount);
    }

    private sealed class BiblePublicationRetryIterationInput
    {
        public required string LanguageCode { get; init; }
        public required string CurrentCategoryName { get; init; }
        public required bool DownloadAll { get; init; }
        public IFetchProgress? Progress { get; init; }
        public required CancellationToken CancellationToken { get; init; }
        public required int Attempt { get; init; }
        public required int RetryDelayBase { get; init; }
        public required int PreviousCatalogedCount { get; init; }
    }

    private static void LogBiblePublicationRetryAttemptDiagnostics(
        int attempt,
        Dictionary<string, BiblePublication>? reQueriedData,
        bool retryHasAllExpected,
        int retryActualCount,
        int retryExpectedCount)
    {
        if (reQueriedData != null)
        {
            var placeholders = reQueriedData.Values.Where(p => !IsPublicationFullyCataloged(p)).Select(p => p.PublicationCode).ToList();

            if (placeholders.Count > 0)
            {
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptStillWaitingForPlaceholders,
                    attempt, placeholders.Count, string.Join(", ", placeholders));
                return;
            }

            if (!retryHasAllExpected)
            {
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptPartialCountWillRetry,
                    attempt, retryActualCount, retryExpectedCount);
            }

            return;
        }

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptNoPublicationsYetWillRetry,
            attempt);
    }

    private sealed record BiblePublicationRetryIterationOutcome(
        bool Completed,
        bool LogSuccess,
        bool BreakRetries,
        Dictionary<string, BiblePublication>? NextSnapshot,
        int ExpectedCountForLog,
        int UpdatedCatalogedCount)
    {
        public static BiblePublicationRetryIterationOutcome ForSuccess(
            Dictionary<string, BiblePublication> reQueried,
            int expectedCount) =>
            new(true, LogSuccess: true, BreakRetries: false, NextSnapshot: reQueried, ExpectedCountForLog: expectedCount,
                UpdatedCatalogedCount: -1);

        public static BiblePublicationRetryIterationOutcome ForStagnation(
            Dictionary<string, BiblePublication>? reQueried) =>
            new(false, LogSuccess: false, BreakRetries: true, NextSnapshot: reQueried, ExpectedCountForLog: 0,
                UpdatedCatalogedCount: -1);

        public static BiblePublicationRetryIterationOutcome ForContinue(
            Dictionary<string, BiblePublication>? fetchedWithProgress,
            int updatedCatalogedCount) =>
            new(false, LogSuccess: false, BreakRetries: false, NextSnapshot: fetchedWithProgress,
                ExpectedCountForLog: 0,
                UpdatedCatalogedCount: updatedCatalogedCount);
    }

    private sealed record DispatchDefaultPublicationFirstPick(
        BiblePublicationSection Section,
        BiblePublicationTrack Track);

    private async Task DispatchDefaultPublicationAsync(
        string languageCode,
        PublicationListViewItemModel defaultPublication,
        string? languageName,
        string? languageDirection)
    {
        try
        {
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationStarting,
                languageCode, defaultPublication.Code);

            var pick =
                await TryGetDispatchDefaultPublicationFirstPickAsync(languageCode, defaultPublication.Code);

            if (pick == null) return;

            // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
            var currentSchedule = state.Value.CurrentSchedule;
            var firstSectionCode = pick.Section.SectionCode;
            var biblePublicationItem = new BiblePublicationStateItem
            {
                CategoryId = currentSchedule?.BiblePublicationCategoryId,
                CategoryName = currentSchedule?.BiblePublicationCategoryName,
                LanguageCode = languageCode,
                PublicationCode = defaultPublication.Code,
                SectionCode = firstSectionCode,
                TrackCode = TrackCodeHelper.GetFromTrack(pick.Track),
                LanguageName = languageName,
                LanguageDirection = languageDirection,
                PublicationName = defaultPublication.Name,
                SectionName = pick.Section.Name,
                TrackTitle = pick.Track.Title
            };

            Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationDispatchingTrackSelected,
                defaultPublication.Code, firstSectionCode, pick.Section.Name, pick.Track.TrackCode, pick.Track.Title);

            dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.ErrorDispatchingDefaultPublicationSelection,
                languageCode, defaultPublication.Code);
        }
    }

    private async Task<DispatchDefaultPublicationFirstPick?> TryGetDispatchDefaultPublicationFirstPickAsync(
        string languageCode,
        string publicationCode)
    {
        var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
        if (sections == null || sections.Count == 0)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationNoSectionsMayBeFlat,
                languageCode, publicationCode);
            return null;
        }

        using var sectionEnumerator = sections.GetEnumerator();
        _ = sectionEnumerator.MoveNext();
        var firstSectionKvp = sectionEnumerator.Current;
        var firstSection = firstSectionKvp.Value;
        var firstSectionIndex = firstSectionKvp.Key;
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationFirstSectionIndexAndName,
            firstSectionIndex, firstSection.Name);

        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSectionIndex);
        if (tracks == null || tracks.Count == 0)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationNoTracksForSection,
                languageCode, publicationCode, firstSectionIndex);
            return null;
        }

        using var trackEnumerator = tracks.Values.GetEnumerator();
        _ = trackEnumerator.MoveNext();
        var firstTrack = trackEnumerator.Current;
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationFirstTrackCodeAndTitle,
            firstTrack.TrackCode, firstTrack.Title);

        return new DispatchDefaultPublicationFirstPick(firstSection, firstTrack);
    }

    private static bool IsPublicationFullyCataloged(BiblePublication p) =>
        !string.IsNullOrEmpty(p.Name) &&
        !string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) &&
        p.Id > 0;

    public Dictionary<string, PublicationListViewItemModel> GetPublicationVMsMapping()
    {
        return publicationVMsMapping;
    }
}
