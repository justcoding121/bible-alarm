#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
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
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly SemaphoreSlim languagePopulationLock = new(1, 1);

    public readonly Dictionary<string, PublicationListViewItemModel> publicationVMsMapping = [];

    public BiblePublicationSelectionDataProvider(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.mediaService = mediaService;
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

            Log.Debug("PopulateLanguagesAsync: Loaded {LanguageCount} languages from GetBiblePublicationLanguages, currentLanguageCode={CurrentLanguageCode}",
                languagesData.Count, currentLanguageCode ?? "(null)");

            var languageVMs = new List<LanguageListViewItemModel>();

            foreach (var language in languagesData.Values
                         .Where(x => trimmedSearchTerm == null
                                     || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Name))
            {
                var languageVm = new LanguageListViewItemModel(language);
                languageVMs.Add(languageVm);

                if (!string.IsNullOrEmpty(currentLanguageCode) && 
                    string.Equals(languageVm.Code, currentLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    languageVm.IsSelected = true;
                    Log.Debug("PopulateLanguagesAsync: Marked language {LanguageCode} ({LanguageName}) as selected",
                        languageVm.Code, languageVm.Name);
                }
            }

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
            Log.Error("PopulatePublicationsAsync: Category is null or empty. Category must always be selected. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, currentPublicationCode);
            throw new InvalidOperationException($"Category must always be selected. No category found in state or provided parameter. LanguageCode={languageCode}, PublicationCode={currentPublicationCode}");
        }

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (publicationVMs, newMapping, defaultPublication) = await Task.Run(async () =>
        {
            // downloadAll=true when publication modal opens (download all publications with first sections and tracks)
            // downloadAll=false when language changes (only download first publication in cascade)
            Dictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication>? publicationsData = null;
            
            // Retry logic: If downloadAll=true and non-English, retry fetching until all publications are harvested
            // For non-English languages, publications need to be fetched, so we retry with increasing delays
            if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                // Up to 10 retries
                const int maxRetries = 10;
                // Start with 1 second
                var retryDelay = 1000;
                // Total max wait time of 60 seconds
                var maxWaitTime = TimeSpan.FromSeconds(60);
                var startTime = DateTime.UtcNow;
                var allHarvested = false;
                var attempt = 0;
                
                Log.Information("PopulatePublicationsAsync: Starting fetch with retries for language={LanguageCode}, category={CategoryName}",
                    languageCode, currentCategoryName);
                
                while (!allHarvested && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
                {
                    attempt++;
                    
                    try
                    {
                        // Fetch publications (this triggers harvesting if needed)
                        publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll);
                        
                        // Wait a bit for background harvesting to start
                        await Task.Delay(500);
                        
                        // Re-query to check if publications are now harvested
                        var reQueriedData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, progress);
                        
                        // Check if ALL publications are harvested (not placeholders)
                        // A publication is harvested if it has a name that's different from its code and has an ID > 0
                        var hasPublications = reQueriedData.Values.Count > 0;
                        var allHarvestedCheck = hasPublications && reQueriedData.Values.All(p => 
                            !string.IsNullOrEmpty(p.Name) && 
                            p.Name != p.PublicationCode && 
                            p.Id > 0);
                        
                        if (allHarvestedCheck)
                        {
                            publicationsData = reQueriedData;
                            allHarvested = true;
                            Log.Information("PopulatePublicationsAsync: All {Count} publications harvested on attempt {Attempt} for language={LanguageCode}",
                                publicationsData.Count, attempt, languageCode);
                        }
                        else
                        {
                            // Log which publications are still placeholders for debugging
                            var placeholders = reQueriedData.Values.Where(p => 
                                string.IsNullOrEmpty(p.Name) || 
                                p.Name == p.PublicationCode || 
                                p.Id == 0).Select(p => p.PublicationCode).ToList();
                            
                            if (placeholders.Count > 0)
                            {
                                Log.Debug("PopulatePublicationsAsync: Attempt {Attempt}: Still waiting for {Count} publications to be harvested: {Placeholders}",
                                    attempt, placeholders.Count, string.Join(", ", placeholders));
                            }
                            else if (!hasPublications)
                            {
                                Log.Debug("PopulatePublicationsAsync: Attempt {Attempt}: No publications found yet, will retry",
                                    attempt);
                            }
                            
                            // Wait with increasing delay before retrying (1s, 2s, 3s, etc., up to 5s)
                            var delay = Math.Min(retryDelay * attempt, 5000);
                            await Task.Delay(delay);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "PopulatePublicationsAsync: Attempt {Attempt} failed for language={LanguageCode}, will retry",
                            attempt, languageCode);
                        
                        // Wait before retrying on exception
                        var delay = Math.Min(retryDelay * attempt, 5000);
                        await Task.Delay(delay);
                    }
                }
                
                if (!allHarvested)
                {
                    Log.Warning("PopulatePublicationsAsync: Timeout after {Attempts} attempts waiting for all publications to be harvested for language {LanguageCode}. Some may still be placeholders.",
                        attempt, languageCode);
                    
                    // Use the last fetched data even if not all are harvested
                    if (publicationsData == null || publicationsData.Count == 0)
                    {
                        // Final attempt to get at least some data
                        try
                        {
                            publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false, progress);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "PopulatePublicationsAsync: Final fetch attempt failed for language={LanguageCode}",
                                languageCode);
                        }
                    }
                }
            }
            else
            {
                // For English or when downloadAll=false, just fetch once
                publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll, progress);
            }
            
            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();

            if (publicationsData == null)
            {
                return (vms, mapping, null);
            }

            foreach (var publication in publicationsData.Values)
            {
                // Skip duplicates - if code already exists, use the existing one
                if (mapping.TryGetValue(publication.PublicationCode, out var existingVm))
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

            // Sort publications by publication-code priority for display (nwt first), then by name.
            vms = PublicationSortHelper.SortByPriority(vms, p => p.Code, p => p.Name).ToList();
            
            // Log sorted order for debugging
            Log.Debug("PopulatePublicationsAsync: Sorted {Count} publications. First 3: {FirstThree}",
                vms.Count,
                string.Join(", ", vms.Take(3).Select(p => $"{p.Code}(priority={PublicationSortHelper.GetPublicationSortPriority(p.Code)})")));

            // Determine default publication: after sorting, the first item is the preferred default (nwt first).
            var preferredDefault = vms.FirstOrDefault();

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
                                    currentSchedule.BiblePublicationTrackNumber == 1;

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
            Log.Debug("DispatchDefaultPublicationAsync: Starting for language={LanguageCode}, publication={PublicationCode}",
                languageCode, defaultPublication.Code);

            var sections = await mediaService.GetBiblePublicationSections(languageCode, defaultPublication.Code);
            if (sections == null || sections.Count == 0)
            {
                Log.Warning("DispatchDefaultPublicationAsync: No sections found for language={LanguageCode}, publication={PublicationCode}. This publication may not have section data.",
                    languageCode, defaultPublication.Code);
                return;
            }

            var firstSectionKvp = sections.First();
            var firstSection = firstSectionKvp.Value;
            // Use the dictionary key (parsed from SectionCode)
            var firstSectionIndex = firstSectionKvp.Key;
            Log.Debug("DispatchDefaultPublicationAsync: First section index={SectionIndex}, name={SectionName}",
                firstSectionIndex, firstSection.Name);

            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, defaultPublication.Code, firstSectionIndex);
            if (tracks == null || tracks.Count == 0)
            {
                Log.Warning("DispatchDefaultPublicationAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, sectionIndex={SectionIndex}",
                    languageCode, defaultPublication.Code, firstSectionIndex);
                return;
            }

            var firstTrack = tracks.Values.First();
            Log.Debug("DispatchDefaultPublicationAsync: First track number={TrackNumber}, title={TrackTitle}",
                firstTrack.Number, firstTrack.Title);

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
                TrackNumber = firstTrack.Number,
                LanguageName = languageName,
                LanguageDirection = languageDirection,
                PublicationName = defaultPublication.Name,
                SectionName = firstSection.Name,
                TrackTitle = firstTrack.Title
            };

            Log.Information("DispatchDefaultPublicationAsync: Dispatching TrackSelectedAction for publication={PublicationCode}, section={SectionCode}/{SectionName}, track={TrackNumber}/{TrackTitle}",
                defaultPublication.Code, firstSectionCode, firstSection.Name, firstTrack.Number, firstTrack.Title);
            
            dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "BibleSelectionDataProvider: Error dispatching default publication selection for language={LanguageCode}, publication={PublicationCode}",
                languageCode, defaultPublication.Code);
        }
    }

    public Dictionary<string, PublicationListViewItemModel> GetPublicationVMsMapping()
    {
        return publicationVMsMapping;
    }
}
