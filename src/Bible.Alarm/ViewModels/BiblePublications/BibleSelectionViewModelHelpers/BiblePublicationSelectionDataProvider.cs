#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
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

            // Do ALL processing on background thread to avoid blocking spinner animation
            // Languages ARE filtered by category - show only languages that have publications in the selected category
            var languageVMs = await Task.Run(async () =>
            {
                var languagesData = await mediaService.GetBiblePublicationLanguages(currentCategoryName);
                var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

                Log.Debug("PopulateLanguagesAsync: Loaded {LanguageCount} languages from GetBiblePublicationLanguages",
                    languagesData.Count);

                var vms = new List<LanguageListViewItemModel>();

                foreach (var language in languagesData.Values
                             .Where(x => trimmedSearchTerm == null
                                         || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(x => x.Name))
                {
                    var languageVm = new LanguageListViewItemModel(language);
                    vms.Add(languageVm);

                    if (!string.IsNullOrEmpty(currentLanguageCode) && languageVm.Code == currentLanguageCode)
                    {
                        languageVm.IsSelected = true;
                    }
                }

                return vms;
            });

            // Add items in small batches with frequent yields for smooth spinner animation
            const int batchSize = 15;
            await MainThread.InvokeOnMainThreadAsync(() => languages.Clear());
            await Task.Yield(); // Let spinner animate after clear

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

    // Priority codes for Bible publications (lower = higher priority)
    private static readonly string[] PriorityPublicationCodes = ["nwt", "bi12"];

    /// <summary>
    /// Gets the sort priority for a publication code.
    /// nwt (2013 NWT) = 0, bi12 (1984 NWT) = 1, others = 2
    /// </summary>
    private static int GetPublicationSortPriority(string code)
    {
        var lowerCode = code.ToLowerInvariant();
        for (int i = 0; i < PriorityPublicationCodes.Length; i++)
        {
            if (lowerCode == PriorityPublicationCodes[i])
                return i;
        }
        return PriorityPublicationCodes.Length; // Others come after priority publications
    }

    public async Task PopulatePublicationsAsync(
        string languageCode,
        ObservableCollection<PublicationListViewItemModel>? publications,
        bool languageChanged,
        bool downloadAll = false,
        string? categoryName = null)
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
            var publicationsData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll);
            
            // If downloadAll=true, wait for harvesting to complete, then re-query to get actual publication names
            if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                // Wait a bit for background harvesting to start
                await Task.Delay(100);
                
                // Wait for harvesting to complete by checking if publications are now available
                // Re-query to get actual publications with correct names (not placeholders)
                var maxWaitTime = TimeSpan.FromSeconds(30);
                var startTime = DateTime.UtcNow;
                var harvested = false;
                
                while (!harvested && (DateTime.UtcNow - startTime) < maxWaitTime)
                {
                    var reQueriedData = await mediaService.GetBiblePublications(languageCode, currentCategoryName, downloadAll: false);
                    
                    // Check if ALL publications are harvested (not placeholders)
                    // A publication is harvested if it has a name that's different from its code and has an ID > 0
                    var allHarvested = reQueriedData.Values.All(p => 
                        !string.IsNullOrEmpty(p.Name) && 
                        p.Name != p.PublicationCode && 
                        p.Id > 0);
                    
                    // Also check that we have at least one publication (to avoid false positives when list is empty)
                    var hasPublications = reQueriedData.Values.Count > 0;
                    
                    if (allHarvested && hasPublications)
                    {
                        publicationsData = reQueriedData;
                        harvested = true;
                        Log.Debug("PopulatePublicationsAsync: All publications harvested, using actual publications with correct names");
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
                            Log.Debug("PopulatePublicationsAsync: Still waiting for {Count} publications to be harvested: {Placeholders}",
                                placeholders.Count, string.Join(", ", placeholders));
                        }
                        
                        // Wait a bit more before checking again
                        await Task.Delay(500);
                    }
                }
                
                if (!harvested)
                {
                    Log.Warning("PopulatePublicationsAsync: Timeout waiting for all publications to be harvested for language {LanguageCode}. Some may still be placeholders.", languageCode);
                }
            }
            
            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();

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

            // Sort publications: nwt first, then bi12, then others by name
            vms = vms
                .OrderBy(p => GetPublicationSortPriority(p.Code))
                .ThenBy(p => p.Name)
                .ToList();

            // Determine default publication: prefer nwt, then bi12, then first available
            PublicationListViewItemModel? preferredDefault = null;
            foreach (var priorityCode in PriorityPublicationCodes)
            {
                if (mapping.TryGetValue(priorityCode, out var priorityPub))
                {
                    preferredDefault = priorityPub;
                    break;
                }
            }
            // Fall back to first publication if no priority publications found
            preferredDefault ??= vms.FirstOrDefault();

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
                var alreadyMatches = currentSchedule != null &&
                                    currentSchedule.BiblePublicationLanguageCode == languageCode &&
                                    currentSchedule.BiblePublicationCode == defaultPublication.Code &&
                                    currentSchedule.BiblePublicationSectionNumber == 1 &&
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
            var firstSectionNumber = firstSectionKvp.Key; // Use the dictionary key (parsed from SectionCode)
            Log.Debug("DispatchDefaultPublicationAsync: First section number={SectionNumber}, name={SectionName}",
                firstSectionNumber, firstSection.Name);

            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, defaultPublication.Code, firstSectionNumber);
            if (tracks == null || tracks.Count == 0)
            {
                Log.Warning("DispatchDefaultPublicationAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, section={SectionNumber}",
                    languageCode, defaultPublication.Code, firstSectionNumber);
                return;
            }

            var firstTrack = tracks.Values.First();
            Log.Debug("DispatchDefaultPublicationAsync: First track number={TrackNumber}, title={TrackTitle}",
                firstTrack.Number, firstTrack.Title);

            // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
            var currentSchedule = state.Value.CurrentSchedule;
            var biblePublicationItem = new BiblePublicationStateItem
            {
                CategoryId = currentSchedule?.BiblePublicationCategoryId,
                CategoryName = currentSchedule?.BiblePublicationCategoryName,
                LanguageCode = languageCode,
                PublicationCode = defaultPublication.Code,
                SectionNumber = firstSectionNumber,
                TrackNumber = firstTrack.Number,
                LanguageName = languageName,
                LanguageDirection = languageDirection,
                PublicationName = defaultPublication.Name,
                SectionName = firstSection.Name,
                TrackTitle = firstTrack.Title
            };

            Log.Information("DispatchDefaultPublicationAsync: Dispatching TrackSelectedAction for publication={PublicationCode}, section={SectionNumber}/{SectionName}, track={TrackNumber}/{TrackTitle}",
                defaultPublication.Code, firstSectionNumber, firstSection.Name, firstTrack.Number, firstTrack.Title);
            
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
