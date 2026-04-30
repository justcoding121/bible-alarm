#nullable enable
using System.Collections.ObjectModel;
using System.Threading;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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

            var languageVMs = new List<LanguageListViewItemModel>();

            foreach (var language in languagesData.Values)
            {
                var name = names.GetValueOrDefault(language.Id) ?? language.LanguageCode;
                if (trimmedSearchTerm != null && !name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                    continue;
                var languageVm = new LanguageListViewItemModel(language, name);
                languageVMs.Add(languageVm);

                if (!string.IsNullOrEmpty(currentLanguageCode) && 
                    string.Equals(languageVm.Code, currentLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    languageVm.IsSelected = true;
                    Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulateLanguagesMarkedSelected,
                        languageVm.Code, languageVm.Name);
                }
            }

            languageVMs = languageVMs.OrderBy(x => x.Name).ToList();

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

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (publicationVMs, newMapping, defaultPublication) = await Task.Run(async () =>
        {
            // downloadAll=true when publication modal opens (download all publications with first sections and tracks)
            // downloadAll=false when language changes (only download first publication in cascade)
            Dictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication>? publicationsData = null;

            if (downloadAll)
            {
                // Always check DB first: if all expected publications are already cataloged, use that and skip fetch/progress
                var initialPublications = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, null);
                var expectedPublicationCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, currentCategoryName);
                var actualPublicationCount = initialPublications?.Values.Count ?? 0;
                var hasAllExpected = actualPublicationCount >= expectedPublicationCount;
                var allPublicationsCataloged = hasAllExpected && initialPublications != null && initialPublications.Values.Count > 0 && initialPublications.Values.All(p =>
                    !string.IsNullOrEmpty(p.Name) && p.Name != p.PublicationCode && p.Id > 0);

                if (allPublicationsCataloged)
                {
                    Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedAlreadyCatalogedSkippingFetch,
                        expectedPublicationCount, languageCode, currentCategoryName);
                    publicationsData = initialPublications;
                }
                else
                {
                    // Not pre-cataloged: show progress and fetch (with retry until cataloged or timeout)
                    var cancellationToken = progress?.CancellationToken ?? CancellationToken.None;
                    // Publications are not fully cataloged - show progress and retry fetching
                    // Up to 10 retries
                    const int maxRetries = 10;
                    // Start with 1 second
                    var retryDelay = 1000;
                    // Total max wait time of 60 seconds
                    var maxWaitTime = TimeSpan.FromSeconds(60);
                    var startTime = DateTime.UtcNow;
                    var allCataloged = false;
                    var attempt = 0;
                    var previousCatalogedCount = -1;
                    
                    Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsStartingFetchWithRetries,
                        languageCode, currentCategoryName);
                    
                    // Show progress overlay at the start of retry loop and keep it visible throughout all retries
                    progress?.SetIsVisible(true);
                    progress?.UpdateProgress(0.0);
                    
                    try
                    {
                        while (!allCataloged && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
                    {
                        // Check for cancellation before each attempt
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        attempt++;
                        
                        try
                        {
                            // Fetch publications (this triggers cataloging if needed)
                            // Pass progress to show download percentage during cataloging
                            publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll, progress);
                            
                            // Wait a bit for background cataloging to start (with cancellation support)
                            await Task.Delay(500, cancellationToken);
                            
                            // Re-query to check if publications are now cataloged (no progress needed for re-query)
                            var reQueriedData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, null);
                            
                            // Get expected publication count to verify we have all publications
                            var retryExpectedCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, currentCategoryName);
                            var retryActualCount = reQueriedData?.Values.Count ?? 0;
                            
                            // Check if we have ALL expected publications AND they're all cataloged (not placeholders)
                            // A publication is cataloged if it has a name that's different from its code and has an ID > 0
                            var retryHasAllExpected = retryActualCount >= retryExpectedCount;
                            var retryAllCataloged = retryHasAllExpected && reQueriedData != null && reQueriedData.Values.Count > 0 && reQueriedData.Values.All(p => 
                                !string.IsNullOrEmpty(p.Name) && 
                                p.Name != p.PublicationCode && 
                                p.Id > 0);
                            
                            if (retryAllCataloged)
                            {
                                publicationsData = reQueriedData;
                                allCataloged = true;
                                Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedCatalogedOnAttempt,
                                    retryExpectedCount, attempt, languageCode, currentCategoryName);
                            }
                            else
                            {
                                var currentCatalogedCount = reQueriedData?.Values.Count(p =>
                                    !string.IsNullOrEmpty(p.Name) && p.Name != p.PublicationCode && p.Id > 0) ?? 0;

                                if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
                                {
                                    Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsNoProgressBetweenRetriesStopping,
                                        currentCatalogedCount, retryExpectedCount, languageCode, currentCategoryName);
                                    publicationsData = reQueriedData;
                                    break;
                                }
                                previousCatalogedCount = currentCatalogedCount;

                                // Log which publications are still placeholders or missing for debugging
                                if (reQueriedData != null)
                                {
                                    var placeholders = reQueriedData.Values.Where(p => 
                                        string.IsNullOrEmpty(p.Name) || 
                                        p.Name == p.PublicationCode || 
                                        p.Id == 0).Select(p => p.PublicationCode).ToList();
                                    
                                    if (placeholders.Count > 0)
                                    {
                                        Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptStillWaitingForPlaceholders,
                                            attempt, placeholders.Count, string.Join(", ", placeholders));
                                    }
                                    else if (!retryHasAllExpected)
                                    {
                                        Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptPartialCountWillRetry,
                                            attempt, retryActualCount, retryExpectedCount);
                                    }
                                }
                                else
                                {
                                    Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptNoPublicationsYetWillRetry,
                                        attempt);
                                }
                                
                                // Wait with increasing delay before retrying (1s, 2s, 3s, etc., up to 5s) - with cancellation support
                                var delay = Math.Min(retryDelay * attempt, 5000);
                                await Task.Delay(delay, cancellationToken);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Re-throw cancellation - data saved so far is preserved
                            Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsFetchCancelledAtAttempt,
                                attempt, languageCode);
                            throw;
                        }
                        catch (System.Net.Http.HttpRequestException)
                        {
                            throw;
                        }
                        catch (System.Net.Sockets.SocketException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (NetworkExceptionHelper.IsNetworkFailure(ex))
                            {
                                throw;
                            }

                            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptFailedWillRetry,
                                attempt, languageCode);
                            
                            // Wait before retrying on exception (with cancellation support)
                            var delay = Math.Min(retryDelay * attempt, 5000);
                            await Task.Delay(delay, cancellationToken);
                        }
                    }
                    }
                    finally
                    {
                        // Hide progress overlay when retry loop completes (success, timeout, or cancellation)
                        progress?.SetIsVisible(false);
                    }
                    
                    if (!allCataloged)
                    {
                        Log.Warning(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsTimeoutWaitingForCatalog,
                            attempt, languageCode);
                        
                        // Use the last fetched data even if not all are cataloged
                        if (publicationsData == null || publicationsData.Count == 0)
                        {
                            // Final attempt to get at least some data
                            try
                            {
                                publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, progress);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsFinalFetchAttemptFailed,
                                    languageCode);
                            }
                        }
                    }
                }
            }
            else
            {
                // downloadAll=false (e.g. language change): single fetch
                publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll, progress);
            }
            
            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();

            if (publicationsData == null)
            {
                return (vms, mapping, null);
            }

            // Remove placeholder publications that couldn't be fetched (e.g. no tracks on the server)
            var unfetchableCodes = publicationsData
                .Where(kvp => kvp.Value.Id == 0 || string.IsNullOrEmpty(kvp.Value.Name) || kvp.Value.Name == kvp.Value.PublicationCode)
                .Select(kvp => kvp.Key)
                .ToList();
            if (unfetchableCodes.Count > 0)
            {
                foreach (var code in unfetchableCodes)
                {
                    publicationsData.Remove(code);
                }
                Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsRemovedUnfetchablePlaceholders,
                    unfetchableCodes.Count, string.Join(", ", unfetchableCodes));
            }

            foreach (var publication in publicationsData.Values)
            {
                // Skip duplicates - if code already exists, use the existing one
                if (mapping.TryGetValue(publication.PublicationCode, out _))
                {
                    // Don't set IsSelected here - it will be set later by SetSelectedPublication()
                    continue;
                }

                var publicationVm = new PublicationListViewItemModel(publication);
                vms.Add(publicationVm);
                mapping[publicationVm.Code] = publicationVm;

                // Don't set IsSelected here - it will be set later by SetSelectedPublication()
                // This ensures only one publication is selected at a time
            }

            // Sort publications using category-specific comparer (Bible: nwt first; Magazine: latest year first; etc.)
            vms = PublicationSortHelper.SortByPriorityForCategory(vms, p => p.Code, p => p.Name, currentCategoryName).ToList();
            
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsSortedFirstThreeCodes,
                vms.Count, currentCategoryName,
                string.Join(", ", vms.Take(3).Select(p => p.Code)));

            // Determine default publication: after sorting, the first item is the preferred default (nwt first).
            var preferredDefault = vms.Count > 0 ? vms[0] : null;

            return (vms, mapping, preferredDefault);
        });

        // Update mapping
        publicationVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            publicationVMsMapping[kvp.Key] = kvp.Value;
        }

            // Handle language change default selection
            // IMPORTANT: Only dispatch default publication if user hasn't already selected a publication
            // This prevents overwriting user's selection (e.g., when switching from "melodies" to "original songs")
            if (languageChanged && defaultPublication != null)
            {
                var currentSchedule = state.Value.CurrentSchedule;
                
                // Check if user has already selected a publication for this language
                // If so, don't dispatch default publication - preserve user's selection
                var userHasSelectedPublication = currentSchedule != null &&
                                                !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode) &&
                                                currentSchedule.BiblePublicationLanguageCode == languageCode;
                
                if (userHasSelectedPublication)
                {
                    // User has already selected a publication - don't dispatch default
                    // Don't set IsSelected here - SetSelectedPublication() will handle it based on current schedule
                }
                else
                {
                    // No publication selected yet - dispatch default publication
                    // Don't set IsSelected here - SetSelectedPublication() will handle it after dispatch

                // Check if state already matches what we're about to dispatch
                var currentSectionCode = currentSchedule?.BiblePublicationSectionCode;
                var alreadyMatches = currentSchedule != null &&
                                    currentSchedule.BiblePublicationLanguageCode == languageCode &&
                                    currentSchedule.BiblePublicationCode == defaultPublication.Code &&
                                    string.Equals(currentSectionCode, "1", StringComparison.OrdinalIgnoreCase) &&
                                    currentSchedule.BiblePublicationTrackCode == "1";

                if (!alreadyMatches)
                {
                    // Fire and forget the dispatch (already on background thread)
                    _ = DispatchDefaultPublicationAsync(languageCode, defaultPublication, currentLanguageName, currentLanguageDirection);
                }
            }
        }

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

            var sections = await mediaService.GetBiblePublicationSections(languageCode, defaultPublication.Code);
            if (sections == null || sections.Count == 0)
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationNoSectionsMayBeFlat,
                    languageCode, defaultPublication.Code);
                return;
            }

            using var sectionEnumerator = sections.GetEnumerator();
            _ = sectionEnumerator.MoveNext();
            var firstSectionKvp = sectionEnumerator.Current;
            var firstSection = firstSectionKvp.Value;
            // Use the dictionary key (parsed from SectionCode)
            var firstSectionIndex = firstSectionKvp.Key;
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationFirstSectionIndexAndName,
                firstSectionIndex, firstSection.Name);

            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, defaultPublication.Code, firstSectionIndex);
            if (tracks == null || tracks.Count == 0)
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationNoTracksForSection,
                    languageCode, defaultPublication.Code, firstSectionIndex);
                return;
            }

            using var trackEnumerator = tracks.Values.GetEnumerator();
            _ = trackEnumerator.MoveNext();
            var firstTrack = trackEnumerator.Current;
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationFirstTrackCodeAndTitle,
                firstTrack.TrackCode, firstTrack.Title);

            // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
            var currentSchedule = state.Value.CurrentSchedule;
            var firstSectionCode = firstSection.SectionCode;
            var biblePublicationItem = new BiblePublicationStateItem
            {
                CategoryId = currentSchedule?.BiblePublicationCategoryId,
                CategoryName = currentSchedule?.BiblePublicationCategoryName,
                LanguageCode = languageCode,
                PublicationCode = defaultPublication.Code,
                SectionCode = firstSectionCode,
                TrackCode = TrackCodeHelper.GetFromTrack(firstTrack),
                LanguageName = languageName,
                LanguageDirection = languageDirection,
                PublicationName = defaultPublication.Name,
                SectionName = firstSection.Name,
                TrackTitle = firstTrack.Title
            };

            Log.Information(AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationDispatchingTrackSelected,
                defaultPublication.Code, firstSectionCode, firstSection.Name, firstTrack.TrackCode, firstTrack.Title);
            
            dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.ErrorDispatchingDefaultPublicationSelection,
                languageCode, defaultPublication.Code);
        }
    }

    public Dictionary<string, PublicationListViewItemModel> GetPublicationVMsMapping()
    {
        return publicationVMsMapping;
    }
}
