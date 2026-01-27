#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles cascade selection logic for Bible publications.
/// Cascade order: Language → Publication → Section → Track
/// </summary>
public sealed class BiblePublicationSelectionItemSelector
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;

    // Using centralized sorting helper from Bible.Alarm.Shared.Helpers.PublicationSortHelper

    public BiblePublicationSelectionItemSelector(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IBiblePublicationService? biblePublicationService = null,
        IBiblePublicationSectionService? biblePublicationSectionService = null,
        ILanguageContentService? languageContentService = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.biblePublicationService = biblePublicationService;
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.languageContentService = languageContentService;
        this.scopeFactory = scopeFactory ?? ServiceProviderManager.GetService<IServiceScopeFactory>() 
            ?? throw new InvalidOperationException("IServiceScopeFactory is required but not available");
    }

    /// <summary>
    /// When user selects a publication, cascade to get first section and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> 
        GetSectionAndTrackForPublicationAsync(
            PublicationListViewItemModel publication,
            LanguageListViewItemModel language,
            IFetchProgress? progress = null)
    {
        Log.Debug("GetSectionAndTrackForPublicationAsync: Starting for publication={PublicationCode}, language={LanguageCode}",
            publication.Code, language.Code);

        // Show progress while fetching
        progress?.UpdateProgress(0.1);
        
        // First, try to get sections from the database
            var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publication.Code));

        progress?.UpdateProgress(0.3);

        // If publication has sections, use sectioned flow
        if (sections != null && sections.Count > 0)
        {
            Log.Debug("GetSectionAndTrackForPublicationAsync: Found {SectionCount} sections, using sectioned flow",
                sections.Count);
            progress?.UpdateProgress(0.5);
            var sectionedResult = await GetFirstSectionAndTrackFromSectionsAsync(language.Code, publication.Code, sections, progress);
            Log.Debug("GetSectionAndTrackForPublicationAsync: Sectioned result: sectionNumber={SectionNumber}, trackNumber={TrackNumber}, sectionName={SectionName}, trackTitle={TrackTitle}",
                sectionedResult.SectionNumber, sectionedResult.TrackNumber, sectionedResult.SectionName, sectionedResult.TrackTitle);
            
            // Warn if names are empty but numbers are valid
            if (sectionedResult.SectionNumber > 0 && string.IsNullOrWhiteSpace(sectionedResult.SectionName))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: SectionName is empty for sectionNumber={SectionNumber}", 
                    sectionedResult.SectionNumber);
            }
            if (sectionedResult.TrackNumber > 0 && string.IsNullOrWhiteSpace(sectionedResult.TrackTitle))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: TrackTitle is empty for trackNumber={TrackNumber}", 
                    sectionedResult.TrackNumber);
            }
            
            return sectionedResult;
        }

        // No sections found - this is a non-sectioned publication (drama/video)
        Log.Debug("GetSectionAndTrackForPublicationAsync: No sections found, using non-sectioned flow");
        progress?.UpdateProgress(0.5);
        var result = await GetFirstTrackForNonSectionedAsync(language.Code, publication.Code, progress);
        Log.Debug("GetSectionAndTrackForPublicationAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            result.TrackNumber, result.TrackTitle);
        return result;
    }

    /// <summary>
    /// When user selects a language, cascade to get first publication, section, and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(string? PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPublicationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language, IFetchProgress? progress = null)
    {
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Starting for language={LanguageCode}", language.Code);

        progress?.UpdateProgress(0.1);

        // Step 1: Get publications and find the first one that can be queried with a language
        // Some publications (like "iam" for Music) have LanguageId = NULL and can't be queried with a language
        var stateValue = state.Value;
        var categoryName = stateValue.CurrentSchedule?.BiblePublicationCategoryName;
        
        // Get all available publications for this language and category
        var publications = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code, categoryName, downloadAll: false, progress));

        if (publications == null || publications.Count == 0)
        {
            Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: No publications found for language={LanguageCode}, category={CategoryName}", 
                language.Code, categoryName ?? "(null)");
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        // Find the first publication - prioritize publications without LanguageId (like "iam" for Music)
        // Publications without LanguageId don't need a language, so the language row should be hidden
        string? publicationCode = null;
        BiblePublication? publication = null;
        bool publicationWithoutLanguage = false;
        
        if (biblePublicationService != null)
        {
            // Iterate through publications in priority order: nwt first, then bi12, then others
            foreach (var pubKvp in PublicationSortHelper.SortByPriority(publications))
            {
                var pubCode = pubKvp.Key;
                var pub = pubKvp.Value;
                
                // Check if this publication has LanguageId = null (doesn't need a language)
                // This is data-driven, not hard-coded
                if (pub.LanguageId == null)
                {
                    // Publication without language - select it and clear language
                    publicationCode = pubCode;
                    publication = pub;
                    publicationWithoutLanguage = true;
                    Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Selected publication={PublicationCode} (has LanguageId=null, doesn't need language)",
                        publicationCode);
                    break;
                }
                
                // Publication has LanguageId - check if already harvested, then harvest if needed
                if (languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        progress?.UpdateProgress(0.3);
                        
                        // Check if publication with first section and tracks is already harvested
                        var isAlreadyHarvested = await CheckIfPublicationWithFirstSectionHarvestedAsync(
                            pubCode, language.Code);
                        
                        if (!isAlreadyHarvested)
                        {
                            // Harvest the publication (EnsurePublicationExistsAsync checks if it exists first)
                            await languageContentService.EnsurePublicationExistsAsync(pubCode, language.Code, default, progress);
                        }
                        else
                        {
                            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Publication={PublicationCode} for language={LanguageCode} already harvested with first section and tracks",
                                pubCode, language.Code);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to harvest publication={PublicationCode} for language={LanguageCode}, trying next", 
                            pubCode, language.Code);
                        continue;
                    }
                }
                
                // Check if this publication can be queried with a language (has LanguageId)
                // Re-query from database to get the actual publication with correct localized name (not placeholder)
                BiblePublication? queriedPub = null;
                var canQueryWithLanguage = await Task.Run(async () =>
                {
                    queriedPub = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(language.Code, pubCode);
                    return queriedPub != null;
                });
                
                if (canQueryWithLanguage && queriedPub != null)
                {
                    publicationCode = pubCode;
                    publication = queriedPub; // Use the queried publication with correct localized name, not the placeholder
                    Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Selected publication={PublicationCode} (can be queried with language={LanguageCode})",
                        publicationCode, language.Code);
                    break;
                }
                else
                {
                    Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Skipping publication={PublicationCode} (cannot be queried with language={LanguageCode})",
                        pubCode, language.Code);
                }
            }
        }
        else
        {
            // Fallback: use first publication if biblePublicationService is not available
            var firstPub = publications.First();
            publicationCode = firstPub.Key;
            publication = firstPub.Value;
            publicationWithoutLanguage = publication.LanguageId == null;
        }

        if (string.IsNullOrEmpty(publicationCode) || publication == null)
        {
            Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: No publication found for language={LanguageCode}, category={CategoryName}", 
                language.Code, categoryName ?? "(null)");
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var publicationName = publication.Name;

        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Selected publication code={PublicationCode}, name={PublicationName}, withoutLanguage={WithoutLanguage}",
            publicationCode, publicationName, publicationWithoutLanguage);

        // For publications without LanguageId, query without a language code
        // For publications with LanguageId, use the language code
        string? languageCodeForQuery = publicationWithoutLanguage ? null : language.Code;

        // Ensure publication is harvested before getting sections
        // For publications with LanguageId, EnsurePublicationExistsAsync should have been called above,
        // but we ensure it here as well to handle edge cases
        if (!publicationWithoutLanguage && languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Ensuring publication {PublicationCode} exists for language {LanguageCode} before getting sections",
                publicationCode, language.Code);
            progress?.UpdateProgress(0.5);
            try
            {
                await languageContentService.EnsurePublicationExistsAsync(publicationCode, language.Code, default, progress);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "GetPublicationSectionAndTrackForLanguageAsync: Failed to ensure publication {PublicationCode} exists, continuing anyway",
                    publicationCode);
            }
        }

        // Try to get sections to determine if publication is sectioned or not
        // IMPORTANT: Query sections directly from database first to avoid triggering full harvesting
        // Only fetch sections if they don't exist yet
        SortedDictionary<int, BiblePublicationSection>? sections = null;
        if (publicationWithoutLanguage)
        {
            // Query sections for publications without language (LanguageId=null)
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Querying sections for publication without language={PublicationCode}", publicationCode);
            sections = await Task.Run(async () =>
                await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode));
        }
        else
        {
            // Query sections directly from database without triggering EnsureAllSectionsForPublicationAsync
            // This avoids fetching all sections when we only need to check if the publication is sectioned
            if (biblePublicationSectionService != null)
            {
                sections = await Task.Run(async () =>
                    await biblePublicationSectionService.GetSectionsByPublicationAsync(language.Code, publicationCode, default));
                
                // If no sections found and publication was just harvested, it might be a non-sectioned publication
                // Or the publication might not exist yet - in that case, EnsurePublicationExistsAsync above should have created it
                // For sectioned publications, EnsurePublicationExistsAsync fetches all sections, so we should have sections now
                if (sections == null || sections.Count == 0)
                {
                    Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found in database after ensuring publication exists, publication is likely non-sectioned");
                }
            }
            else
            {
                // Fallback to MediaService method (but this will trigger full harvesting)
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: biblePublicationSectionService is null, using MediaService.GetBiblePublicationSections (will trigger full harvesting)");
                sections = await Task.Run(async () =>
                    await mediaService.GetBiblePublicationSections(language.Code, publicationCode));
            }
        }

        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get first section and track
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Found {SectionCount} sections, using sectioned flow", sections.Count);
            progress?.UpdateProgress(0.7);
            // For publications without language, pass null as language code
            var (sectionNumber, firstTrackNumber, sectionName, firstTrackTitle) = 
                await GetFirstSectionAndTrackFromSectionsAsync(languageCodeForQuery ?? string.Empty, publicationCode, sections, progress);

            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Sectioned result: sectionNumber={SectionNumber}, sectionName={SectionName}, trackNumber={TrackNumber}, trackTitle={TrackTitle}",
                sectionNumber, sectionName, firstTrackNumber, firstTrackTitle);

            // Warn if names are empty but numbers are valid
            if (sectionNumber > 0 && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: SectionName is empty for sectionNumber={SectionNumber}", 
                    sectionNumber);
            }
            if (firstTrackNumber > 0 && string.IsNullOrWhiteSpace(firstTrackTitle))
            {
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: TrackTitle is empty for trackNumber={TrackNumber}", 
                    firstTrackNumber);
            }

            return (publicationCode, sectionNumber, firstTrackNumber, sectionName, publicationName, firstTrackTitle);
        }

        // Non-sectioned publication (drama/video) - get first track directly
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found, using non-sectioned flow");
        progress?.UpdateProgress(0.7);
        // For publications without language, pass null/empty as language code
        // GetFirstTrackForNonSectionedAsync will need to handle this case
        var (_, trackNumber, _, trackTitle) = await GetFirstTrackForNonSectionedAsync(languageCodeForQuery ?? string.Empty, publicationCode, progress);
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            trackNumber, trackTitle);
        return (publicationCode, 0, trackNumber, string.Empty, publicationName, trackTitle);
    }

    /// <summary>
    /// Gets first section and first track for a sectioned publication.
    /// </summary>
    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstSectionAndTrackAsync(string languageCode, string publicationCode)
    {
        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(languageCode, publicationCode));

        if (sections == null || sections.Count == 0)
        {
            return (0, 0, string.Empty, string.Empty);
        }

        return await GetFirstSectionAndTrackFromSectionsAsync(languageCode, publicationCode, sections);
    }

    /// <summary>
    /// Gets first section and first track from pre-loaded sections.
    /// Handles both publications with LanguageId (requires language code) and without LanguageId (no language needed).
    /// </summary>
    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstSectionAndTrackFromSectionsAsync(string languageCode, string publicationCode, SortedDictionary<int, BiblePublicationSection> sections, IFetchProgress? progress = null)
    {
        var firstSectionKvp = sections.First();
        var firstSection = firstSectionKvp.Value;
        var firstSectionNumber = firstSectionKvp.Key; // Use the dictionary key (parsed from SectionCode)
        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: First section number={SectionNumber}, name={SectionName}",
            firstSectionNumber, firstSection.Name);

        SortedDictionary<int, BiblePublicationTrack>? tracks = null;
        
        // Check if this is a publication without LanguageId (empty languageCode indicates this)
        if (string.IsNullOrEmpty(languageCode))
        {
            // Query tracks directly from database for publications without LanguageId
            tracks = await Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
                
                var pub = await dbContext.BiblePublications
                    .AsNoTracking()
                    .Include(x => x.Sections)
                        .ThenInclude(s => s.Tracks)
                    .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                    .FirstOrDefaultAsync();
                
                if (pub?.Sections != null)
                {
                    var section = pub.Sections.FirstOrDefault(s => s.SectionCode == firstSection.SectionCode);
                    if (section?.Tracks != null && section.Tracks.Count > 0)
                    {
                        var tracksDict = section.Tracks
                            .OrderBy(t => t.Number)
                            .ToDictionary(t => t.Number, t => t);
                        return new SortedDictionary<int, BiblePublicationTrack>(tracksDict);
                    }
                }
                
                return new SortedDictionary<int, BiblePublicationTrack>();
            });
        }
        else
        {
            // Query tracks using language code (normal case)
            // Use the actual SectionCode from the section object to ensure correct matching
            // After ad-hoc harvesting, tracks might not be found if we use the parsed section number
            // Instead, query directly using the section's SectionCode
            tracks = await Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
                
                var pub = await dbContext.BiblePublications
                    .AsNoTracking()
                    .Include(x => x.Language)
                    .Include(x => x.Sections)
                        .ThenInclude(s => s.Tracks)
                    .Where(x => x.PublicationCode == publicationCode && 
                               x.Language != null && 
                               x.Language.LanguageCode == languageCode.ToUpperInvariant())
                    .FirstOrDefaultAsync();
                
                SortedDictionary<int, BiblePublicationTrack>? foundTracks = null;
                
                if (pub?.Sections != null)
                {
                    // Use the actual SectionCode from the section object for matching (case-insensitive)
                    var section = pub.Sections.FirstOrDefault(s => 
                        s.SectionCode.Equals(firstSection.SectionCode, StringComparison.OrdinalIgnoreCase));
                    if (section?.Tracks != null && section.Tracks.Count > 0)
                    {
                        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: Found {TrackCount} tracks for section={SectionCode} using direct query",
                            section.Tracks.Count, section.SectionCode);
                        var tracksDict = section.Tracks
                            .OrderBy(t => t.Number)
                            .ToDictionary(t => t.Number, t => t);
                        foundTracks = new SortedDictionary<int, BiblePublicationTrack>(tracksDict);
                    }
                    else
                    {
                        Log.Warning("GetFirstSectionAndTrackFromSectionsAsync: Section found but no tracks. SectionCode={SectionCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                            firstSection.SectionCode, publicationCode, languageCode);
                    }
                }
                else
                {
                    Log.Warning("GetFirstSectionAndTrackFromSectionsAsync: Publication not found or has no sections. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        publicationCode, languageCode);
                }
                
                // If no tracks found in database, explicitly fetch them (matching cascade behavior)
                // Tracks are NOT automatically fetched when sections are harvested
                if (foundTracks == null || foundTracks.Count == 0)
                {
                    Log.Information("GetFirstSectionAndTrackFromSectionsAsync: No tracks found in database, fetching tracks for first section...");
                    progress?.UpdateProgress(0.7);
                    if (languageContentService != null)
                    {
                        var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                            publicationCode, firstSection.SectionCode, languageCode);
                        
                        if (fetchSuccess)
                        {
                            // Re-query tracks after fetching
                            Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: Tracks fetched successfully, re-querying from database");
                            return await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSectionNumber);
                        }
                        else
                        {
                            Log.Warning("GetFirstSectionAndTrackFromSectionsAsync: Failed to fetch tracks for section={SectionCode}, publication={PublicationCode}, language={LanguageCode}",
                                firstSection.SectionCode, publicationCode, languageCode);
                        }
                    }
                }
                
                // Return found tracks, or fallback to the original method if fetch didn't work
                if (foundTracks != null && foundTracks.Count > 0)
                {
                    return foundTracks;
                }
                
                Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: Falling back to mediaService.GetBiblePublicationTracks");
                return await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSectionNumber);
            });
        }

        if (tracks == null || tracks.Count == 0)
        {
            Log.Warning("GetFirstSectionAndTrackFromSectionsAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, section={SectionNumber}",
                languageCode ?? "(null)", publicationCode, firstSectionNumber);
            progress?.UpdateProgress(1.0);
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = tracks.Values.First();
        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: First track number={TrackNumber}, title={TrackTitle}",
            firstTrack.Number, firstTrack.Title);
        
        progress?.UpdateProgress(1.0);
        return (firstSectionNumber, firstTrack.Number, firstSection.Name, firstTrack.Title);
    }

    /// <summary>
    /// Gets first track for a non-sectioned publication (drama/video).
    /// Handles both publications with LanguageId (requires language code) and without LanguageId (no language needed).
    /// </summary>
    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstTrackForNonSectionedAsync(string languageCode, string publicationCode, IFetchProgress? progress = null)
    {
        Log.Debug("GetFirstTrackForNonSectionedAsync: Starting for language={LanguageCode}, publication={PublicationCode}, biblePublicationService={HasService}",
            languageCode, publicationCode, biblePublicationService != null);

        if (biblePublicationService == null)
        {
            Log.Warning("GetFirstTrackForNonSectionedAsync: biblePublicationService is null, returning empty result");
            return (0, 0, string.Empty, string.Empty);
        }

        BiblePublication? publication;
        
        // Check if this is a publication without LanguageId (empty languageCode indicates this)
        if (string.IsNullOrEmpty(languageCode))
        {
            // Query publication without LanguageId (for music publications like "iam")
            publication = await Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
                
                // Query publication with LanguageId == null
                var pub = await dbContext.BiblePublications
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                    .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                    .FirstOrDefaultAsync();
                
                return pub;
            });
        }
        else
        {
            // Query publication with LanguageId (normal case)
            publication = await Task.Run(async () =>
                await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode));
        }

        Log.Debug("GetFirstTrackForNonSectionedAsync: Loaded publication={PublicationName}, TracksCount={TracksCount}",
            publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0);

        // If no tracks found, ensure publication is harvested (for non-sectioned publications, this fetches tracks)
        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Information("GetFirstTrackForNonSectionedAsync: No tracks found in database, ensuring publication exists (will fetch tracks for non-sectioned publications)...");
            
            // For non-sectioned publications, EnsurePublicationExistsAsync should fetch tracks
            if (!string.IsNullOrEmpty(languageCode) && languageContentService != null)
            {
                progress?.UpdateProgress(0.6);
                var harvestSuccess = await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode, default, progress);
                
                if (harvestSuccess)
                {
                    // Re-query tracks after harvesting
                    Log.Debug("GetFirstTrackForNonSectionedAsync: Publication harvested successfully, re-querying tracks");
                    progress?.UpdateProgress(0.8);
                    publication = await Task.Run(async () =>
                        await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode));
                }
                else
                {
                    Log.Warning("GetFirstTrackForNonSectionedAsync: Failed to harvest publication={PublicationCode} for language={LanguageCode}",
                        publicationCode, languageCode);
                }
            }
            else if (string.IsNullOrEmpty(languageCode))
            {
                // For publications without language (like "iam"), tracks should already be pre-harvested
                // If not found, log a warning
                Log.Warning("GetFirstTrackForNonSectionedAsync: No tracks found for publication without language={PublicationCode}",
                    publicationCode);
            }
        }

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Warning("GetFirstTrackForNonSectionedAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}",
                languageCode ?? "(null)", publicationCode);
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = publication.Tracks.OrderBy(t => t.Number).First();
        Log.Information("GetFirstTrackForNonSectionedAsync: Found first track Number={TrackNumber}, Title={TrackTitle}",
            firstTrack.Number, firstTrack.Title);
        
        progress?.UpdateProgress(1.0);
        return (0, firstTrack.Number, string.Empty, firstTrack.Title);
    }

    /// <summary>
    /// Checks if a publication with its first section and tracks is already harvested.
    /// </summary>
    private async Task<bool> CheckIfPublicationWithFirstSectionHarvestedAsync(
        string publicationCode,
        string languageCode)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = publicationCode;
            }

            // Check if publication exists
            var publication = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.Tracks)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode);

            if (publication == null)
            {
                return false;
            }

            // Get first section code from SectionLanguages
            var firstSectionCode = await db.SectionLanguages
                .AsNoTracking()
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language != null &&
                           sl.Language.LanguageCode == normalizedLanguageCode)
                .OrderBy(sl => sl.SectionCode)
                .Select(sl => sl.SectionCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(firstSectionCode))
            {
                // No sections defined - check if it's a flat publication (has tracks directly)
                var hasTracks = await db.BiblePublicationTracks
                    .AsNoTracking()
                    .Include(t => t.Publication)
                        .ThenInclude(bp => bp!.Language)
                    .AnyAsync(t => t.Publication != null &&
                                   t.Publication.PublicationCode == publicationCodeForDb &&
                                   t.Publication.Language != null &&
                                   t.Publication.Language.LanguageCode == normalizedLanguageCode &&
                                   t.BiblePublicationSectionId == null);
                return hasTracks;
            }

            // Check if first section exists with tracks
            var firstSection = publication.Sections
                .FirstOrDefault(s => s.SectionCode.Equals(firstSectionCode, StringComparison.OrdinalIgnoreCase));

            if (firstSection == null)
            {
                return false;
            }

            // Check if section has tracks
            return firstSection.Tracks != null && firstSection.Tracks.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CheckIfPublicationWithFirstSectionHarvestedAsync: Error checking if publication {PublicationCode} is harvested",
                publicationCode);
            return false;
        }
    }
}
