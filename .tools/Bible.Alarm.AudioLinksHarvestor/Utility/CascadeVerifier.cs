#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

/// <summary>
/// Verifies that database data matches cascade logic expectations for all categories.
/// </summary>
public sealed class CascadeVerifier
{
    private readonly MediaDbContext dbContext;
    private readonly ILogger logger;
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly IBiblePublicationTrackService? biblePublicationTrackService;

    public CascadeVerifier(MediaDbContext dbContext, ILogger logger, IServiceScopeFactory? scopeFactory = null)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.scopeFactory = scopeFactory;
        
        // Initialize services for ad-hoc harvesting tests (same services used by bible container)
        if (scopeFactory != null)
        {
            using var scope = scopeFactory.CreateScope();
            var httpClient = scope.ServiceProvider.GetService<HttpClient>();
            if (httpClient != null)
            {
                this.biblePublicationService = scope.ServiceProvider.GetService<IBiblePublicationService>();
                this.languageContentService = scope.ServiceProvider.GetService<ILanguageContentService>();
                this.biblePublicationSectionService = scope.ServiceProvider.GetService<IBiblePublicationSectionService>();
                this.biblePublicationTrackService = scope.ServiceProvider.GetService<IBiblePublicationTrackService>();
            }
        }
    }

    // Helper methods to mimic MediaService behavior using Shared services directly
    private async Task<Dictionary<string, BiblePublication>> GetBiblePublicationsAsync(string languageCode, string? categoryName, bool downloadAll = false)
    {
        if (biblePublicationService == null || languageContentService == null)
            return new Dictionary<string, BiblePublication>();

        // Get available publication codes
        var availableCodes = await biblePublicationService.GetAvailablePublicationCodesAsync(languageCode, categoryName);
        
        // Get downloaded publications
        var downloaded = await biblePublicationService.GetByLanguageCodeAsync(languageCode, categoryName);
        
        var result = new Dictionary<string, BiblePublication>();
        foreach (var pub in downloaded.Values)
        {
            result[pub.PublicationCode] = pub;
        }

        // If downloadAll, ensure all publications are harvested
        if (downloadAll)
        {
            await languageContentService.EnsureAllPublicationsForLanguageAsync(languageCode, categoryName);
            // Re-query after download
            downloaded = await biblePublicationService.GetByLanguageCodeAsync(languageCode, categoryName);
            result.Clear();
            foreach (var pub in downloaded.Values)
            {
                result[pub.PublicationCode] = pub;
            }
        }

        return result;
    }

    private async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSectionsAsync(string languageCode, string publicationCode)
    {
        if (biblePublicationSectionService == null || languageContentService == null)
            return new SortedDictionary<int, BiblePublicationSection>();

        // Ensure all sections are harvested for non-English
        if (!string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            await languageContentService.EnsureAllSectionsForPublicationAsync(publicationCode, languageCode);
        }

        var sections = await biblePublicationSectionService.GetSectionsByPublicationAsync(languageCode, publicationCode, default);
        // Sections already come as SortedDictionary, so return directly
        return sections;
    }

    private async Task<SortedDictionary<int, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguageAsync(string publicationCode)
    {
        if (biblePublicationSectionService == null)
            return new SortedDictionary<int, BiblePublicationSection>();

        var sections = await biblePublicationSectionService.GetSectionsByPublicationWithoutLanguageAsync(publicationCode, default);
        // Sections already come as SortedDictionary, so return directly
        return sections;
    }

    private async Task<SortedDictionary<int, BiblePublicationTrack>> GetBiblePublicationTracksAsync(string languageCode, string publicationCode, int sectionNumber)
    {
        if (biblePublicationTrackService == null)
            return new SortedDictionary<int, BiblePublicationTrack>();

        var tracks = await biblePublicationTrackService.GetTracksBySectionAsync(languageCode, publicationCode, sectionNumber, default);
        // Tracks already come as SortedDictionary, so return directly
        return tracks;
    }

    private async Task<Dictionary<string, Language>> GetBiblePublicationLanguagesAsync(string? categoryName = null)
    {
        if (biblePublicationService == null)
            return new Dictionary<string, Language>();

        return await biblePublicationService.GetDistinctLanguagesAsync(categoryName);
    }

    public async Task<bool> VerifyAsync()
    {
        logger.Information("=== Verifying Database Cascade Logic ===");

        // First, clean up any "iam" entries from PublicationLanguages
        await CleanupIamFromPublicationLanguagesAsync();

        var categories = new[] { "Bible", "Dramas", "Music" };
        bool allPassed = true;

        foreach (var category in categories)
        {
            logger.Information("--- Category: {Category} ---", category);
            bool categoryPassed = await VerifyCategoryAsync(category);
            if (!categoryPassed)
            {
                allPassed = false;
            }
        }

        // Ad-hoc harvesting verification tests
        if (scopeFactory != null && biblePublicationService != null && languageContentService != null)
        {
            logger.Information("=== Testing Ad-Hoc Harvesting ===");
            bool adhocPassed = await VerifyAdHocHarvestingAsync();
            if (!adhocPassed)
            {
                allPassed = false;
            }
        }
        else
        {
            logger.Warning("=== Skipping Ad-Hoc Harvesting Tests (services not available) ===");
        }

        if (allPassed)
        {
            logger.Information("=== All Verifications Passed ===");
        }
        else
        {
            logger.Warning("=== Some Verifications Failed ===");
        }

        return allPassed;
    }

    private async Task CleanupIamFromPublicationLanguagesAsync()
    {
        var iamEntries = await dbContext.PublicationLanguages
            .Where(pl => pl.PublicationCode == "iam")
            .ToListAsync();

        if (iamEntries.Count > 0)
        {
            logger.Information("Removing {Count} 'iam' entry/entries from PublicationLanguages (instrumental music should not be in PublicationLanguages)", iamEntries.Count);
            dbContext.PublicationLanguages.RemoveRange(iamEntries);
            await dbContext.SaveChangesAsync();
            logger.Information("✓ Cleaned up 'iam' entries from PublicationLanguages");
        }
    }

    private async Task<bool> VerifyCategoryAsync(string categoryName)
    {
        bool passed = true;

        // Step 1: Get first language (prefer "E" if available)
        var languagesQuery = dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Category != null && pl.Category.CategoryName == categoryName && pl.Language != null)
            .Select(pl => pl.Language!)
            .Distinct();

        var languages = await languagesQuery.ToListAsync();
        if (languages.Count == 0)
        {
            logger.Warning("  No languages found for category {Category}", categoryName);
            return false;
        }

        var firstLanguage = languages.FirstOrDefault(l => l.LanguageCode == "E") ?? languages.First();
        logger.Information("  First Language: {LanguageCode} ({LanguageName})", 
            firstLanguage.LanguageCode, firstLanguage.Name);

        // Step 2: Get publications by Id order from PublicationLanguages
        var publicationLanguages = await dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && 
                        pl.Language.LanguageCode == firstLanguage.LanguageCode &&
                        pl.Category != null && 
                        pl.Category.CategoryName == categoryName)
            .OrderBy(pl => pl.Id)
            .ToListAsync();

        logger.Information("  Publications in PublicationLanguages (by Id order):");
        foreach (var pl in publicationLanguages)
        {
            logger.Information("    - Id: {Id}, Code: {PublicationCode}", pl.Id, pl.PublicationCode);
        }

        // Step 3: Find first publication that can be queried with the language (has LanguageId in BiblePublications)
        string? firstQueryablePub = null;
        foreach (var pl in publicationLanguages)
        {
            var canQueryWithLanguage = await dbContext.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == pl.PublicationCode &&
                               bp.LanguageId != null &&
                               bp.Language != null &&
                               bp.Language.LanguageCode == firstLanguage.LanguageCode);

            if (canQueryWithLanguage)
            {
                firstQueryablePub = pl.PublicationCode;
                logger.Information("  First Queryable Publication: {PublicationCode}", firstQueryablePub);
                break;
            }
        }

        if (string.IsNullOrEmpty(firstQueryablePub))
        {
            logger.Warning("  WARNING: No queryable publication found for language {LanguageCode} in category {Category}",
                firstLanguage.LanguageCode, categoryName);
            passed = false;
        }

        // Step 4: Category-specific validations
        if (categoryName == "Dramas")
        {
            passed = await VerifyDramasAsync(firstLanguage.LanguageCode) && passed;
        }
        else if (categoryName == "Music")
        {
            passed = await VerifyMusicAsync() && passed;
        }

        return passed;
    }

    private async Task<bool> VerifyDramasAsync(string languageCode)
    {
        logger.Information("  Drama-specific checks:");
        bool passed = true;

        // In PublicationLanguages, dramas are stored with their API codes: "Dramas", "DramaticBibleReadings", "gnj"
        // In BiblePublications, "Dramas" is stored as "Bible Dramas" (the display name)
        var expectedDramas = new[] { ("Dramas", "Dramas"), ("DramaticBibleReadings", "DramaticBibleReadings"), ("gnj", "gnj") };

        foreach (var (apiCode, pubLangCode) in expectedDramas)
        {
            var exists = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .AnyAsync(pl => pl.PublicationCode == pubLangCode &&
                               pl.Language != null && pl.Language.LanguageCode == languageCode &&
                               pl.Category != null && pl.Category.CategoryName == "Dramas");

            if (exists)
            {
                logger.Information("    ✓ {PubLangCode} found in PublicationLanguages", pubLangCode);
            }
            else
            {
                logger.Warning("    ✗ {PubLangCode} NOT found in PublicationLanguages", pubLangCode);
                passed = false;
            }

            // Check for duplicates (case variations)
            var duplicates = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.PublicationCode.ToLower() == pubLangCode.ToLower() &&
                            pl.PublicationCode != pubLangCode &&
                            pl.Language != null && pl.Language.LanguageCode == languageCode &&
                            pl.Category != null && pl.Category.CategoryName == "Dramas")
                .ToListAsync();

            if (duplicates.Count > 0)
            {
                logger.Warning("    ✗ WARNING: Duplicate case variations found for {PubLangCode}: {Duplicates}",
                    pubLangCode, string.Join(", ", duplicates.Select(d => d.PublicationCode)));
                passed = false;
            }
        }

        return passed;
    }

    private async Task<bool> VerifyMusicAsync()
    {
        logger.Information("  Music-specific checks:");
        bool passed = true;

        // Check that "iam" (Kingdom Melodies) has LanguageId = NULL
        var iamWithNullLanguage = await dbContext.BiblePublications
            .AsNoTracking()
            .CountAsync(bp => bp.PublicationCode == "iam" && bp.LanguageId == null);

        if (iamWithNullLanguage > 0)
        {
            logger.Information("    ✓ 'iam' (Kingdom Melodies) correctly has LanguageId = NULL");
        }
        else
        {
            logger.Warning("    ✗ 'iam' (Kingdom Melodies) does NOT have LanguageId = NULL");
            passed = false;
        }

        // Check that "iam" is NOT in PublicationLanguages (since it's instrumental)
        var iamInPubLang = await dbContext.PublicationLanguages
            .AsNoTracking()
            .CountAsync(pl => pl.PublicationCode == "iam");

        if (iamInPubLang == 0)
        {
            logger.Information("    ✓ 'iam' correctly NOT in PublicationLanguages (instrumental music)");
        }
        else
        {
            logger.Warning("    ✗ WARNING: 'iam' found in PublicationLanguages (should not be there for instrumental music)");
            passed = false;
        }

        return passed;
    }

    /// <summary>
    /// Verifies ad-hoc harvesting scenarios using the same methods as bible container rows and modals.
    /// Tests scenarios where user selects unharvested data (language, publication, section).
    /// </summary>
    private async Task<bool> VerifyAdHocHarvestingAsync()
    {
        if (biblePublicationService == null || languageContentService == null)
        {
            logger.Warning("  Ad-hoc harvesting services not available");
            return false;
        }

        bool allPassed = true;

        // Test 1: Select an unharvested language from bible container
        // Find a language that exists in PublicationLanguages but doesn't have content in BiblePublications
        logger.Information("  Test 1: Selecting unharvested language from bible container");
        var unharvestedLanguage = await dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && 
                        pl.Language.LanguageCode != "E" &&
                        pl.Category != null && 
                        pl.Category.CategoryName == "Bible")
            .Select(pl => pl.Language!)
            .Distinct()
            .FirstOrDefaultAsync();

        if (unharvestedLanguage != null)
        {
            logger.Information("    Testing with language: {LanguageCode} ({LanguageName})", 
                unharvestedLanguage.LanguageCode, unharvestedLanguage.Name);

            // Check if publication exists before harvesting
            var publicationsBefore = await biblePublicationService.GetAvailablePublicationCodesAsync(
                unharvestedLanguage.LanguageCode, "Bible");
            
            logger.Information("    Publications before harvest: {Count}", publicationsBefore.Count);

            // Get first publication code for this language from PublicationLanguages
            var firstPublicationCode = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null && 
                            pl.Language.LanguageCode == unharvestedLanguage.LanguageCode &&
                            pl.Category != null && 
                            pl.Category.CategoryName == "Bible")
                .OrderBy(pl => pl.Id)
                .Select(pl => pl.PublicationCode)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(firstPublicationCode))
            {
                // Use the same method as bible container: EnsurePublicationExistsAsync
                logger.Information("    Harvesting publication {PublicationCode} for language {LanguageCode}...", 
                    firstPublicationCode, unharvestedLanguage.LanguageCode);
                
                var harvestSuccess = await languageContentService.EnsurePublicationExistsAsync(
                    firstPublicationCode, unharvestedLanguage.LanguageCode);

                if (harvestSuccess)
                {
                    // Verify publication now exists
                    var publication = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                        unharvestedLanguage.LanguageCode, firstPublicationCode);

                    if (publication != null)
                    {
                        logger.Information("    ✓ Publication {PublicationCode} successfully harvested with {SectionCount} sections", 
                            firstPublicationCode, publication.Sections?.Count ?? 0);
                    }
                    else
                    {
                        logger.Warning("    ✗ Publication {PublicationCode} harvest reported success but not found in database", 
                            firstPublicationCode);
                        allPassed = false;
                    }
                }
                else
                {
                    logger.Warning("    ✗ Failed to harvest publication {PublicationCode} for language {LanguageCode}", 
                        firstPublicationCode, unharvestedLanguage.LanguageCode);
                    allPassed = false;
                }
            }
        }
        else
        {
            logger.Warning("    No unharvested language found for testing");
        }

        // Test 2: Open publications list view for unharvested language
        logger.Information("  Test 2: Opening publications list view for unharvested language");
        var unharvestedLanguage2 = await dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && 
                        pl.Language.LanguageCode != "E" &&
                        pl.Category != null && 
                        pl.Category.CategoryName == "Bible")
            .Select(pl => pl.Language!)
            .Distinct()
            .Skip(1)
            .FirstOrDefaultAsync();

        if (unharvestedLanguage2 != null)
        {
            logger.Information("    Testing with language: {LanguageCode} ({LanguageName})", 
                unharvestedLanguage2.LanguageCode, unharvestedLanguage2.Name);

            // Use the same method as bible container: GetAvailablePublicationCodesAsync
            var availablePublications = await biblePublicationService.GetAvailablePublicationCodesAsync(
                unharvestedLanguage2.LanguageCode, "Bible");

            logger.Information("    Available publications: {Count}", availablePublications.Count);

            // Get publications from PublicationLanguages
            var publicationLanguages = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null && 
                            pl.Language.LanguageCode == unharvestedLanguage2.LanguageCode &&
                            pl.Category != null && 
                            pl.Category.CategoryName == "Bible")
                .Select(pl => pl.PublicationCode)
                .ToListAsync();

            logger.Information("    Publications in PublicationLanguages: {Count}", publicationLanguages.Count);

            // Harvest first unharvested publication
            var unharvestedPub = publicationLanguages.FirstOrDefault(p => !availablePublications.Contains(p));
            if (!string.IsNullOrEmpty(unharvestedPub))
            {
                logger.Information("    Harvesting unharvested publication {PublicationCode}...", unharvestedPub);
                var success = await languageContentService.EnsurePublicationExistsAsync(
                    unharvestedPub, unharvestedLanguage2.LanguageCode);

                if (success)
                {
                    var updatedPublications = await biblePublicationService.GetAvailablePublicationCodesAsync(
                        unharvestedLanguage2.LanguageCode, "Bible");
                    
                    if (updatedPublications.Contains(unharvestedPub))
                    {
                        logger.Information("    ✓ Publication {PublicationCode} successfully harvested and now available", 
                            unharvestedPub);
                    }
                    else
                    {
                        logger.Warning("    ✗ Publication {PublicationCode} harvest reported success but not in available list", 
                            unharvestedPub);
                        allPassed = false;
                    }
                }
                else
                {
                    logger.Warning("    ✗ Failed to harvest publication {PublicationCode}", unharvestedPub);
                    allPassed = false;
                }
            }
        }

        // Test 3: Open section list view for unharvested language
        logger.Information("  Test 3: Opening section list view for unharvested language");
        var unharvestedLanguage3 = await dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && 
                        pl.Language.LanguageCode != "E" &&
                        pl.Category != null && 
                        pl.Category.CategoryName == "Bible")
            .Select(pl => pl.Language!)
            .Distinct()
            .Skip(2)
            .FirstOrDefaultAsync();

        if (unharvestedLanguage3 != null)
        {
            logger.Information("    Testing with language: {LanguageCode} ({LanguageName})", 
                unharvestedLanguage3.LanguageCode, unharvestedLanguage3.Name);

            // Get first publication for this language
            var firstPub = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null && 
                            pl.Language.LanguageCode == unharvestedLanguage3.LanguageCode &&
                            pl.Category != null && 
                            pl.Category.CategoryName == "Bible")
                .OrderBy(pl => pl.Id)
                .Select(pl => pl.PublicationCode)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(firstPub))
            {
                // Harvest publication first
                await languageContentService.EnsurePublicationExistsAsync(
                    firstPub, unharvestedLanguage3.LanguageCode);

                // Use the same method as bible container: GetByLanguageAndCodeWithSectionsAsync
                var publication = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                    unharvestedLanguage3.LanguageCode, firstPub);

                if (publication != null && publication.Sections != null && publication.Sections.Count > 0)
                {
                    logger.Information("    ✓ Publication {PublicationCode} has {SectionCount} sections", 
                        firstPub, publication.Sections.Count);
                    
                    // Verify sections have tracks
                    var sectionWithTracks = publication.Sections.FirstOrDefault(s => s.Tracks != null && s.Tracks.Count > 0);
                    if (sectionWithTracks != null)
                    {
                        logger.Information("    ✓ Section {SectionCode} has {TrackCount} tracks", 
                            sectionWithTracks.SectionCode, sectionWithTracks.Tracks.Count);
                    }
                    else
                    {
                        logger.Warning("    ✗ No sections with tracks found");
                        allPassed = false;
                    }
                }
                else
                {
                    logger.Warning("    ✗ Publication {PublicationCode} has no sections", firstPub);
                    allPassed = false;
                }
            }
        }

        // Test 4: Scenario 1 - User selects new language 'S' (Spanish) for a category
        // This validates the complete cascade: first publication (with null language if applicable), 
        // first section harvested if applicable, and tracks for that section
        // IMPORTANT: All harvesting is filtered by current category (Bible)
        var categoryName = "Bible";
        logger.Information("  Test 4: Scenario 1 - User selects new language 'S' (Spanish) for category '{CategoryName}'", categoryName);
        
        if (languageContentService == null || biblePublicationService == null)
        {
            logger.Warning("    LanguageContentService or BiblePublicationService not available");
            allPassed = false;
        }
        else
        {
            // Step 1: Get publications using same logic as MediaService (same as bible container does)
            // This returns both publications with language and without language (null language)
            var publications = await GetBiblePublicationsAsync("S", categoryName, downloadAll: false);
            
            if (publications != null && publications.Count > 0)
            {
                logger.Information("    Found {Count} publications for Spanish (category: {CategoryName})", 
                    publications.Count, categoryName);

                // Step 2: Find first publication - prioritize publications without LanguageId, then with LanguageId
                // This matches the logic in GetPublicationSectionAndTrackForLanguageAsync
                string? firstPublicationCode = null;
                BiblePublication? firstPublication = null;
                bool publicationWithoutLanguage = false;

                foreach (var pubKvp in publications.OrderBy(p => p.Key))
                {
                    var pubCode = pubKvp.Key;
                    var pub = pubKvp.Value;

                    // Check if this publication has LanguageId = null (doesn't need a language)
                    if (pub.LanguageId == null)
                    {
                        firstPublicationCode = pubCode;
                        firstPublication = pub;
                        publicationWithoutLanguage = true;
                        logger.Information("    Selected publication without LanguageId: {PublicationCode}", pubCode);
                        break;
                    }

                    // Publication has LanguageId - try to harvest if needed
                    var harvestSuccess = await languageContentService.EnsurePublicationExistsAsync(pubCode, "S");
                    if (harvestSuccess)
                    {
                        // Verify it can be queried with language
                        var queriedPub = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync("S", pubCode);
                        if (queriedPub != null)
                        {
                            firstPublicationCode = pubCode;
                            firstPublication = queriedPub;
                            logger.Information("    Selected publication with LanguageId: {PublicationCode}", pubCode);
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(firstPublicationCode) || firstPublication == null)
                {
                    logger.Warning("    ✗ No valid publication found for Spanish (category: {CategoryName})", categoryName);
                    allPassed = false;
                }
                else
                {
                    logger.Information("    First publication (category: {CategoryName}): {PublicationCode}", 
                        categoryName, firstPublicationCode);

                    // Step 3: Get sections using MediaService (same as bible container does)
                    // This automatically harvests all sections if not English
                    SortedDictionary<int, BiblePublicationSection>? sections = null;
                    if (publicationWithoutLanguage)
                    {
                        // For publications without language, use GetSectionsForPublicationWithoutLanguage
                        sections = await GetSectionsForPublicationWithoutLanguageAsync(firstPublicationCode);
                    }
                    else
                    {
                        sections = await GetBiblePublicationSectionsAsync("S", firstPublicationCode);
                    }

                    if (sections != null && sections.Count > 0)
                    {
                        logger.Information("    ✓ Found {Count} sections", sections.Count);

                        // Step 4: Get first section and tracks using MediaService
                        var firstSectionKvp = sections.First();
                        var firstSection = firstSectionKvp.Value;
                        var firstSectionNumber = firstSectionKvp.Key;

                        logger.Information("    ✓ First section: Number={Number}, Code={Code}, Name={Name}", 
                            firstSectionNumber, firstSection.SectionCode, firstSection.Name);

                        // Step 5: Get tracks for first section using MediaService
                        // IMPORTANT: Tracks are NOT automatically fetched when sections are harvested
                        // They are only fetched when GetBiblePublicationTracks is called, but that method
                        // only queries the database. If tracks don't exist, we need to explicitly fetch them.
                        SortedDictionary<int, BiblePublicationTrack>? tracks = null;
                        if (publicationWithoutLanguage)
                        {
                            // For publications without language, tracks are in the publication query
                            var pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(null, firstPublicationCode);
                            if (pubWithTracks?.Sections != null)
                            {
                                var section = pubWithTracks.Sections.FirstOrDefault(s => s.SectionCode == firstSection.SectionCode);
                                if (section?.Tracks != null && section.Tracks.Count > 0)
                                {
                                    tracks = new SortedDictionary<int, BiblePublicationTrack>(
                                        section.Tracks.OrderBy(t => t.Number).ToDictionary(t => t.Number, t => t));
                                }
                            }
                        }
                        else
                        {
                            // First try to get tracks from database
                            tracks = await GetBiblePublicationTracksAsync("S", firstPublicationCode, firstSectionNumber);
                            
                            // If no tracks found, explicitly fetch them (matching cascade behavior)
                            if ((tracks == null || tracks.Count == 0) && languageContentService != null)
                            {
                                logger.Information("    No tracks found in database, fetching tracks for first section...");
                                var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                                    firstPublicationCode, firstSection.SectionCode, "S");
                                
                                if (fetchSuccess)
                                {
                                    // Re-query tracks after fetching
                                    tracks = await GetBiblePublicationTracksAsync("S", firstPublicationCode, firstSectionNumber);
                                }
                            }
                        }

                        if (tracks != null && tracks.Count > 0)
                        {
                            var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                            logger.Information("    ✓ First section has {TrackCount} tracks", tracks.Count);
                            logger.Information("    ✓ First track: Number={Number}, Title={Title}", 
                                firstTrack.Number, firstTrack.Title);

                            // Verification: Check database to confirm data was saved
                            var tracksInDb = await dbContext.BiblePublicationTracks
                                .AsNoTracking()
                                .Include(t => t.Section)
                                    .ThenInclude(s => s.BiblePublication)
                                        .ThenInclude(p => p.Language)
                                .Include(t => t.Section)
                                    .ThenInclude(s => s.BiblePublication)
                                        .ThenInclude(p => p.Category)
                                .Where(t => t.Section != null &&
                                           t.Section.BiblePublication != null &&
                                           t.Section.BiblePublication.PublicationCode == firstPublicationCode &&
                                           t.Section.BiblePublication.Language != null &&
                                           t.Section.BiblePublication.Language.LanguageCode == "S" &&
                                           t.Section.BiblePublication.Category != null &&
                                           t.Section.BiblePublication.Category.CategoryName == categoryName &&
                                           t.Section.SectionCode == firstSection.SectionCode)
                                .CountAsync();

                            if (tracksInDb > 0)
                            {
                                logger.Information("    ✓ Verified: {TrackCount} tracks saved in database", tracksInDb);
                            }
                            else
                            {
                                logger.Warning("    ✗ Verification failed: No tracks found in database");
                                allPassed = false;
                            }
                        }
                        else
                        {
                            logger.Warning("    ✗ First section has no tracks");
                            allPassed = false;
                        }
                    }
                    else
                    {
                        // Non-sectioned publication - check for direct tracks
                        var publicationWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync("S", firstPublicationCode);
                        if (publicationWithTracks != null && publicationWithTracks.Tracks != null && publicationWithTracks.Tracks.Count > 0)
                        {
                            var firstTrack = publicationWithTracks.Tracks.OrderBy(t => t.Number).First();
                            logger.Information("    ✓ Non-sectioned publication - First track: Number={Number}, Title={Title}", 
                                firstTrack.Number, firstTrack.Title);
                        }
                        else
                        {
                            logger.Warning("    ✗ Publication has no sections or tracks");
                            allPassed = false;
                        }
                    }
                }
            }
            else
            {
                logger.Warning("    No publications found for Spanish (category: {CategoryName})", categoryName);
                allPassed = false;
            }
        }


        // Test 5: Scenario 2 - User opens publications modal for a language
        // This validates: download all publications available in that language under current category,
        // harvest tracks (and first section if sectioned) if not already
        // IMPORTANT: All harvesting is filtered by current category (Bible)
        categoryName = "Bible";
        logger.Information("  Test 5: Scenario 2 - User opens publications modal for language 'F' (French), category '{CategoryName}'", categoryName);
        
        if (languageContentService == null || biblePublicationService == null)
        {
            logger.Warning("    LanguageContentService or BiblePublicationService not available");
            allPassed = false;
        }
        else
        {
            // Step 1: Get publications using same logic as MediaService (same as bible container does)
            // This returns both publications with language and without language (null language)
            var publications = await GetBiblePublicationsAsync("F", categoryName, downloadAll: false);
            
            logger.Information("    Found {Count} publications for French (category: {CategoryName})", 
                publications?.Count ?? 0, categoryName);

            if (publications != null && publications.Count > 0)
            {
                // Step 2: Get available publication codes (for verification)
                var availablePublications = await biblePublicationService.GetAvailablePublicationCodesAsync("F", categoryName);
                logger.Information("    Available publication codes: {Count}", availablePublications.Count);

                // Step 3: Simulate opening publications modal - call GetBiblePublications with downloadAll=true
                // This triggers EnsureAllPublicationsForLanguageAsync in background
                logger.Information("    Simulating publications modal open - getting all publications with downloadAll=true (category: {CategoryName})...", 
                    categoryName);
                var allPublications = await GetBiblePublicationsAsync("F", categoryName, downloadAll: true);
                
                logger.Information("    Publications after modal open (category: {CategoryName}): {Count}", 
                    categoryName, allPublications?.Count ?? 0);

                if (allPublications != null && allPublications.Count > 0)
                {
                    logger.Information("    ✓ All available publications in category '{CategoryName}' are now available", categoryName);

                    // Step 4: Verify each publication has first section and tracks (if sectioned) using MediaService
                    foreach (var pubKvp in allPublications)
                    {
                        var publicationCode = pubKvp.Key;
                        var publication = pubKvp.Value;

                        // Verify publication is in the correct category
                        if (publication.Category == null || publication.Category.CategoryName != categoryName)
                        {
                            logger.Warning("    ✗ Publication {PublicationCode} is not in category '{CategoryName}'", 
                                publicationCode, categoryName);
                            allPassed = false;
                            continue;
                        }

                        // Get sections using same logic as MediaService (same as bible container does)
                        SortedDictionary<int, BiblePublicationSection>? sections = null;
                        if (publication.LanguageId == null)
                        {
                            sections = await GetSectionsForPublicationWithoutLanguageAsync(publicationCode);
                        }
                        else
                        {
                            sections = await GetBiblePublicationSectionsAsync("F", publicationCode);
                        }

                        if (sections != null && sections.Count > 0)
                        {
                            // Sectioned publication - get first section and tracks using MediaService
                            var firstSectionKvp = sections.First();
                            var firstSection = firstSectionKvp.Value;
                            var firstSectionNumber = firstSectionKvp.Key;

                            // Get tracks using MediaService
                            SortedDictionary<int, BiblePublicationTrack>? tracks = null;
                            if (publication.LanguageId == null)
                            {
                                // For publications without language, tracks are in the publication query
                                var pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(null, publicationCode);
                                if (pubWithTracks?.Sections != null)
                                {
                                    var section = pubWithTracks.Sections.FirstOrDefault(s => s.SectionCode == firstSection.SectionCode);
                                    if (section?.Tracks != null && section.Tracks.Count > 0)
                                    {
                                        tracks = new SortedDictionary<int, BiblePublicationTrack>(
                                            section.Tracks.OrderBy(t => t.Number).ToDictionary(t => t.Number, t => t));
                                    }
                                }
                            }
                            else
                            {
                                // First try to get tracks from database
                                tracks = await GetBiblePublicationTracksAsync("F", publicationCode, firstSectionNumber);
                                
                                // If no tracks found, explicitly fetch them (matching cascade behavior)
                                if ((tracks == null || tracks.Count == 0) && languageContentService != null)
                                {
                                    var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                                        publicationCode, firstSection.SectionCode, "F");
                                    
                                    if (fetchSuccess)
                                    {
                                        // Re-query tracks after fetching
                                        tracks = await GetBiblePublicationTracksAsync("F", publicationCode, firstSectionNumber);
                                    }
                                }
                            }

                            if (tracks != null && tracks.Count > 0)
                            {
                                var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                                logger.Information("    ✓ Publication {PublicationCode} (category: {CategoryName}): First section {SectionCode} has {TrackCount} tracks", 
                                    publicationCode, categoryName, firstSection.SectionCode, tracks.Count);
                                
                                // Verification: Check database to confirm tracks were saved
                                var tracksInDb = await dbContext.BiblePublicationTracks
                                    .AsNoTracking()
                                    .Include(t => t.Section)
                                        .ThenInclude(s => s.BiblePublication)
                                            .ThenInclude(p => p.Language)
                                    .Include(t => t.Section)
                                        .ThenInclude(s => s.BiblePublication)
                                            .ThenInclude(p => p.Category)
                                    .Where(t => t.Section != null &&
                                               t.Section.BiblePublication != null &&
                                               t.Section.BiblePublication.PublicationCode == publicationCode &&
                                               t.Section.BiblePublication.Language != null &&
                                               t.Section.BiblePublication.Language.LanguageCode == "F" &&
                                               t.Section.BiblePublication.Category != null &&
                                               t.Section.BiblePublication.Category.CategoryName == categoryName &&
                                               t.Section.SectionCode == firstSection.SectionCode)
                                    .CountAsync();

                                if (tracksInDb > 0)
                                {
                                    logger.Information("    ✓ Verified: {TrackCount} tracks saved in database", tracksInDb);
                                }
                                else
                                {
                                    logger.Warning("    ✗ Verification failed: No tracks found in database");
                                    allPassed = false;
                                }
                            }
                            else
                            {
                                logger.Warning("    ✗ Publication {PublicationCode} (category: {CategoryName}): First section {SectionCode} has no tracks", 
                                    publicationCode, categoryName, firstSection.SectionCode);
                                allPassed = false;
                            }
                        }
                        else
                        {
                            // Non-sectioned publication - check for direct tracks using MediaService
                            var publicationWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync("F", publicationCode);

                            if (publicationWithTracks != null && publicationWithTracks.Tracks != null && publicationWithTracks.Tracks.Count > 0)
                            {
                                logger.Information("    ✓ Publication {PublicationCode} (category: {CategoryName}): Non-sectioned, has {TrackCount} direct tracks", 
                                    publicationCode, categoryName, publicationWithTracks.Tracks.Count);
                            }
                            else
                            {
                                logger.Warning("    ✗ Publication {PublicationCode} (category: {CategoryName}): Non-sectioned but has no tracks", 
                                    publicationCode, categoryName);
                                allPassed = false;
                            }
                        }
                    }
                }
                else
                {
                    logger.Warning("    ✗ No publications returned after opening modal (category: {CategoryName})", categoryName);
                    allPassed = false;
                }
            }
            else
            {
                logger.Warning("    No publications found for French (category: {CategoryName})", categoryName);
                allPassed = false;
            }
        }

        // Test 6: Scenario 3 - User opens section modal for a publication
        // This validates: populate all sections in that language if needed, save to db, show sections to user
        // IMPORTANT: Publication must be in the current category (Bible)
        categoryName = "Bible";
        logger.Information("  Test 6: Scenario 3 - User opens section modal for publication 'nwt' in language 'I' (Italian), category '{CategoryName}'", categoryName);
        var italianLanguage = await dbContext.Languages
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LanguageCode == "I");

        if (italianLanguage != null)
        {
            logger.Information("    Testing with language: {LanguageCode} ({LanguageName}), category: {CategoryName}", 
                italianLanguage.LanguageCode, italianLanguage.Name, categoryName);

            var publicationCode = "nwt";
            var languageCode = "I";

            // Step 1: Verify publication is in the correct category before harvesting
            var publicationInCategory = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .AnyAsync(pl => pl.PublicationCode == publicationCode &&
                              pl.Category != null &&
                              pl.Category.CategoryName == categoryName &&
                              pl.Language != null &&
                              pl.Language.LanguageCode == languageCode);

            if (!publicationInCategory)
            {
                logger.Warning("    ✗ Publication {PublicationCode} is not in category '{CategoryName}' for language {LanguageCode}", 
                    publicationCode, categoryName, languageCode);
                allPassed = false;
            }
            else
            {
                logger.Information("    ✓ Publication {PublicationCode} is in category '{CategoryName}'", 
                    publicationCode, categoryName);

                // Step 2: Ensure publication exists first (harvest if needed)
                // This is filtered by category since we verified it's in the correct category
                logger.Information("    Ensuring publication {PublicationCode} exists for language {LanguageCode} (category: {CategoryName})...", 
                    publicationCode, languageCode, categoryName);
                var publicationHarvestSuccess = await languageContentService.EnsurePublicationExistsAsync(
                    publicationCode, languageCode);

                if (publicationHarvestSuccess)
                {
                    // Step 3: Verify publication is in the correct category after harvest
                    var publicationAfterHarvest = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                        languageCode, publicationCode);

                    if (publicationAfterHarvest != null && 
                        (publicationAfterHarvest.Category == null || publicationAfterHarvest.Category.CategoryName != categoryName))
                    {
                        logger.Warning("    ✗ Publication {PublicationCode} is not in category '{CategoryName}' after harvest", 
                            publicationCode, categoryName);
                        allPassed = false;
                    }
                    else
                    {
                        logger.Information("    ✓ Publication {PublicationCode} is in category '{CategoryName}' after harvest", 
                            publicationCode, categoryName);

                        // Step 4: Check sections count before opening section modal
                        // Sections are implicitly filtered by category since they belong to the publication which is in the category
                            var sectionsBefore = await dbContext.BiblePublicationSections
                                .AsNoTracking()
                                .Include(s => s.BiblePublication)
                                    .ThenInclude(p => p.Language)
                                .Include(s => s.BiblePublication)
                                    .ThenInclude(p => p.Category)
                            .Where(s => s.BiblePublication != null &&
                                       s.BiblePublication.PublicationCode == publicationCode &&
                                       s.BiblePublication.Language != null &&
                                       s.BiblePublication.Language.LanguageCode == languageCode &&
                                       s.BiblePublication.Category != null &&
                                       s.BiblePublication.Category.CategoryName == categoryName)
                            .CountAsync();

                        logger.Information("    Sections before opening section modal (category: {CategoryName}): {Count}", 
                            categoryName, sectionsBefore);

                        // Step 5: Simulate opening section modal - call GetBiblePublicationSections
                        // This automatically calls EnsureAllSectionsForPublicationAsync for non-English languages
                        // Sections are filtered by category since they belong to the publication which is in the category
                        logger.Information("    Simulating section modal open - ensuring all sections are downloaded (category: {CategoryName})...", 
                            categoryName);
                        var sectionsHarvestSuccess = await languageContentService.EnsureAllSectionsForPublicationAsync(
                            publicationCode, languageCode);

                        if (sectionsHarvestSuccess)
                        {
                            // Step 6: Verify all sections are now in database (filtered by category)
                            var sectionsAfter = await dbContext.BiblePublicationSections
                                .AsNoTracking()
                                .Include(s => s.BiblePublication)
                                    .ThenInclude(p => p.Language)
                                .Include(s => s.BiblePublication)
                                    .ThenInclude(p => p.Category)
                                .Where(s => s.BiblePublication != null &&
                                           s.BiblePublication.PublicationCode == publicationCode &&
                                           s.BiblePublication.Language != null &&
                                           s.BiblePublication.Language.LanguageCode == languageCode &&
                                           s.BiblePublication.Category != null &&
                                           s.BiblePublication.Category.CategoryName == categoryName)
                                .CountAsync();

                            logger.Information("    Sections after opening section modal (category: {CategoryName}): {Count}", 
                                categoryName, sectionsAfter);

                            if (sectionsAfter > sectionsBefore)
                            {
                                logger.Information("    ✓ Additional sections were harvested ({Before} → {After}) in category '{CategoryName}'", 
                                    sectionsBefore, sectionsAfter, categoryName);

                                // Step 7: Get all sections from database (same as GetBiblePublicationSections does)
                                // Query sections directly from database to simulate what GetBiblePublicationSections returns
                                // Sections are filtered by category since they belong to the publication which is in the category
                                var allSectionsInDb = await dbContext.BiblePublicationSections
                                    .AsNoTracking()
                                    .Include(s => s.BiblePublication)
                                        .ThenInclude(p => p.Language)
                                    .Include(s => s.BiblePublication)
                                        .ThenInclude(p => p.Category)
                                    .Where(s => s.BiblePublication != null &&
                                               s.BiblePublication.PublicationCode == publicationCode &&
                                               s.BiblePublication.Language != null &&
                                               s.BiblePublication.Language.LanguageCode == languageCode &&
                                               s.BiblePublication.Category != null &&
                                               s.BiblePublication.Category.CategoryName == categoryName)
                                    .OrderBy(s => s.SectionCode)
                                    .ToListAsync();

                                if (allSectionsInDb.Count > 0)
                                {
                                    logger.Information("    ✓ Retrieved {Count} sections for display in UI (category: {CategoryName})", 
                                        allSectionsInDb.Count, categoryName);

                                    // Step 8: Verify sections are properly ordered and have section codes
                                    var firstSection = allSectionsInDb.First();
                                    var lastSection = allSectionsInDb.Last();

                                    logger.Information("    ✓ First section: Code={Code}, Name={Name}", 
                                        firstSection.SectionCode, firstSection.Name);
                                    logger.Information("    ✓ Last section: Code={Code}, Name={Name}", 
                                        lastSection.SectionCode, lastSection.Name);

                                    // Step 9: Verify sections are saved in database with correct structure and category
                                    // Check that all sections have required fields and belong to publication in correct category
                                    var sectionsWithValidData = allSectionsInDb
                                        .Where(s => !string.IsNullOrEmpty(s.SectionCode) && 
                                                   !string.IsNullOrEmpty(s.Name) &&
                                                   s.BiblePublication != null &&
                                                   s.BiblePublication.PublicationCode == publicationCode &&
                                                   s.BiblePublication.Category != null &&
                                                   s.BiblePublication.Category.CategoryName == categoryName)
                                        .Count();

                                    if (sectionsWithValidData == allSectionsInDb.Count)
                                    {
                                        logger.Information("    ✓ All {Count} sections are saved in database with valid data (category: {CategoryName})", 
                                            allSectionsInDb.Count, categoryName);
                                    }
                                    else
                                    {
                                        logger.Warning("    ✗ Some sections have invalid data or wrong category: Valid={Valid}, Total={Total}", 
                                            sectionsWithValidData, allSectionsInDb.Count);
                                        allPassed = false;
                                    }
                                }
                                else
                                {
                                    logger.Warning("    ✗ No sections retrieved for display (category: {CategoryName})", categoryName);
                                    allPassed = false;
                                }
                            }
                            else if (sectionsAfter == sectionsBefore && sectionsAfter > 0)
                            {
                                logger.Information("    ✓ All sections were already harvested ({Count} sections, category: {CategoryName})", 
                                    sectionsAfter, categoryName);
                            }
                            else
                            {
                                logger.Warning("    ✗ No sections were harvested or found (category: {CategoryName})", categoryName);
                                allPassed = false;
                            }
                        }
                        else
                        {
                            logger.Warning("    ✗ Failed to ensure all sections are downloaded for publication {PublicationCode} (category: {CategoryName})", 
                                publicationCode, categoryName);
                            allPassed = false;
                        }
                    }
                }
                else
                {
                    logger.Warning("    ✗ Failed to ensure publication {PublicationCode} exists for language {LanguageCode} (category: {CategoryName})", 
                        publicationCode, languageCode, categoryName);
                    allPassed = false;
                }
            }
        }
        else
        {
            logger.Warning("    Language 'I' (Italian) not found in Languages table");
            allPassed = false;
        }

        // Test 7: Scenario 4 - Category change cascade
        // This validates: when user changes category, defaults to English, gets first publication, section, track
        logger.Information("  Test 7: Scenario 4 - Category change cascade (from 'Bible' to 'Dramas')");
        var dramasCategoryName = "Dramas";
        
        if (languageContentService == null || biblePublicationService == null)
        {
            logger.Warning("    LanguageContentService or BiblePublicationService not available");
            allPassed = false;
        }
        else
        {
            logger.Information("    Testing category change to: {CategoryName}", dramasCategoryName);

            // Step 1: Get languages for Dramas category using same logic as MediaService (should default to English "E")
            var languagesForDramas = await GetBiblePublicationLanguagesAsync(dramasCategoryName);
            
            if (languagesForDramas.TryGetValue("E", out var englishLanguage))
            {
                logger.Information("    ✓ English language found for category '{CategoryName}'", dramasCategoryName);

                // Step 2: Get publications using same logic as MediaService (same as bible container does)
                // This returns both publications with language and without language (null language)
                var publications = await GetBiblePublicationsAsync("E", dramasCategoryName, downloadAll: false);
                
                if (publications != null && publications.Count > 0)
                {
                    // Step 3: Find first publication - prioritize publications without LanguageId, then with LanguageId
                    string? firstPublicationCode = null;
                    BiblePublication? firstPublication = null;
                    
                    foreach (var pubKvp in publications.OrderBy(p => p.Key))
                    {
                        var pubCode = pubKvp.Key;
                        var pub = pubKvp.Value;
                        
                        // Check if this publication has LanguageId = null (doesn't need a language)
                        if (pub.LanguageId == null)
                        {
                            firstPublicationCode = pubCode;
                            firstPublication = pub;
                            logger.Information("    Selected publication without LanguageId: {PublicationCode}", pubCode);
                            break;
                        }
                        
                        // Publication has LanguageId - try to harvest if needed
                        var harvestSuccess = await languageContentService.EnsurePublicationExistsAsync(pubCode, "E");
                        if (harvestSuccess)
                        {
                            // Verify it can be queried with language
                            var queriedPub = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync("E", pubCode);
                            if (queriedPub != null && queriedPub.Category != null && queriedPub.Category.CategoryName == dramasCategoryName)
                            {
                                firstPublicationCode = pubCode;
                                firstPublication = queriedPub;
                                logger.Information("    Selected publication with LanguageId: {PublicationCode}", pubCode);
                                break;
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(firstPublicationCode) || firstPublication == null)
                    {
                        logger.Warning("    ✗ No valid publication found for English (category: {CategoryName})", dramasCategoryName);
                        allPassed = false;
                    }
                    else
                    {
                        logger.Information("    First publication for English in category '{CategoryName}': {PublicationCode}", 
                            dramasCategoryName, firstPublicationCode);

                        // Step 4: Get sections using same logic as MediaService
                        SortedDictionary<int, BiblePublicationSection>? sections = null;
                        if (firstPublication.LanguageId == null)
                        {
                            sections = await GetSectionsForPublicationWithoutLanguageAsync(firstPublicationCode);
                        }
                        else
                        {
                            sections = await GetBiblePublicationSectionsAsync("E", firstPublicationCode);
                        }

                        if (sections != null && sections.Count > 0)
                        {
                            var firstSectionKvp = sections.First();
                            var firstSection = firstSectionKvp.Value;
                            var firstSectionNumber = firstSectionKvp.Key;
                            
                            logger.Information("    ✓ First section: Number={Number}, Code={Code}, Name={Name}", 
                                firstSectionNumber, firstSection.SectionCode, firstSection.Name);

                            // Get tracks for first section using same logic as MediaService
                            SortedDictionary<int, BiblePublicationTrack>? tracks = null;
                            if (firstPublication.LanguageId == null)
                            {
                                // For publications without language, tracks are in the publication query
                                var pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(null, firstPublicationCode);
                                if (pubWithTracks?.Sections != null)
                                {
                                    var section = pubWithTracks.Sections.FirstOrDefault(s => s.SectionCode == firstSection.SectionCode);
                                    if (section?.Tracks != null && section.Tracks.Count > 0)
                                    {
                                        tracks = new SortedDictionary<int, BiblePublicationTrack>(
                                            section.Tracks.OrderBy(t => t.Number).ToDictionary(t => t.Number, t => t));
                                    }
                                }
                            }
                            else
                            {
                                // First try to get tracks from database
                                tracks = await GetBiblePublicationTracksAsync("E", firstPublicationCode, firstSectionNumber);
                                
                                // If no tracks found, explicitly fetch them (matching cascade behavior)
                                if ((tracks == null || tracks.Count == 0) && languageContentService != null)
                                {
                                    var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                                        firstPublicationCode, firstSection.SectionCode, "E");
                                    
                                    if (fetchSuccess)
                                    {
                                        // Re-query tracks after fetching
                                        tracks = await GetBiblePublicationTracksAsync("E", firstPublicationCode, firstSectionNumber);
                                    }
                                }
                            }

                            if (tracks != null && tracks.Count > 0)
                            {
                                var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                                logger.Information("    ✓ First section has {TrackCount} tracks", tracks.Count);
                                logger.Information("    ✓ First track: Number={Number}, Title={Title}", 
                                    firstTrack.Number, firstTrack.Title);
                                
                                // Verification: Check database to confirm tracks were saved
                                var tracksInDb = await dbContext.BiblePublicationTracks
                                    .AsNoTracking()
                                    .Include(t => t.Section)
                                        .ThenInclude(s => s.BiblePublication)
                                            .ThenInclude(p => p.Language)
                                    .Include(t => t.Section)
                                        .ThenInclude(s => s.BiblePublication)
                                            .ThenInclude(p => p.Category)
                                    .Where(t => t.Section != null &&
                                               t.Section.BiblePublication != null &&
                                               t.Section.BiblePublication.PublicationCode == firstPublicationCode &&
                                               t.Section.BiblePublication.Language != null &&
                                               t.Section.BiblePublication.Language.LanguageCode == "E" &&
                                               t.Section.BiblePublication.Category != null &&
                                               t.Section.BiblePublication.Category.CategoryName == dramasCategoryName &&
                                               t.Section.SectionCode == firstSection.SectionCode)
                                    .CountAsync();
                                
                                if (tracksInDb > 0)
                                {
                                    logger.Information("    ✓ Verified: {TrackCount} tracks saved in database", tracksInDb);
                                }
                            }
                            else
                            {
                                // Non-sectioned publication - check for direct tracks
                                var publicationWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync("E", firstPublicationCode);
                                if (publicationWithTracks != null && publicationWithTracks.Tracks != null && publicationWithTracks.Tracks.Count > 0)
                                {
                                    var firstTrack = publicationWithTracks.Tracks.OrderBy(t => t.Number).First();
                                    logger.Information("    ✓ Non-sectioned publication - First track: Number={Number}, Title={Title}", 
                                        firstTrack.Number, firstTrack.Title);
                                }
                                else
                                {
                                    logger.Warning("    ✗ Publication has no sections or tracks");
                                    allPassed = false;
                                }
                            }
                        }
                        else
                        {
                            logger.Warning("    ✗ No sections found for publication {PublicationCode}", firstPublicationCode);
                            allPassed = false;
                        }
                    }
                }
                else
                {
                    logger.Warning("    No publications found for English in category '{CategoryName}'", dramasCategoryName);
                    allPassed = false;
                }
            }
            else
            {
                logger.Warning("    English language not found for category '{CategoryName}'", dramasCategoryName);
                allPassed = false;
            }
        }

        // Test 8: Scenario 5 - Publication change cascade
        // This validates: when user selects a publication, gets first section and track
        categoryName = "Bible";
        logger.Information("  Test 8: Scenario 5 - Publication change cascade (selecting 'bi12' in language 'E')");
        var publicationCodeForTest = "bi12";
        var languageCodeForTest = "E";

        // Step 1: Ensure publication exists
        var pubHarvestSuccess = await languageContentService.EnsurePublicationExistsAsync(
            publicationCodeForTest, languageCodeForTest);

        if (pubHarvestSuccess)
        {
            // Step 2: Get publication with sections
            var publicationForCascade = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                languageCodeForTest, publicationCodeForTest);

            if (publicationForCascade != null && publicationForCascade.Category != null &&
                publicationForCascade.Category.CategoryName == categoryName)
            {
                logger.Information("    ✓ Publication {PublicationCode} exists in category '{CategoryName}'", 
                    publicationCodeForTest, categoryName);

                // Step 3: Get first section and track (same as HandlePublicationCascadeAsync does)
                if (publicationForCascade.Sections != null && publicationForCascade.Sections.Count > 0)
                {
                    var firstSection = publicationForCascade.Sections.OrderBy(s => s.SectionCode).First();
                    logger.Information("    ✓ First section: Code={Code}, Name={Name}", 
                        firstSection.SectionCode, firstSection.Name);

                    // Get tracks for first section using same logic as MediaService
                    var sectionNumber = int.TryParse(firstSection.SectionCode, out var num) ? num : 1;
                    var tracks = await GetBiblePublicationTracksAsync(languageCodeForTest, publicationCodeForTest, sectionNumber);
                    
                    if (tracks != null && tracks.Count > 0)
                    {
                        var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                        logger.Information("    ✓ First track: Number={Number}, Title={Title}", 
                            firstTrack.Number, firstTrack.Title);
                    }
                    else
                    {
                        logger.Warning("    ✗ First section has no tracks");
                        allPassed = false;
                    }
                }
                else
                {
                    // Non-sectioned publication
                    var publicationWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                        languageCodeForTest, publicationCodeForTest);

                    if (publicationWithTracks != null && publicationWithTracks.Tracks != null && publicationWithTracks.Tracks.Count > 0)
                    {
                        var firstTrack = publicationWithTracks.Tracks.OrderBy(t => t.Number).First();
                        logger.Information("    ✓ Non-sectioned publication - First track: Number={Number}, Title={Title}", 
                            firstTrack.Number, firstTrack.Title);
                    }
                    else
                    {
                        logger.Warning("    ✗ Publication has no sections or tracks");
                        allPassed = false;
                    }
                }
            }
            else
            {
                logger.Warning("    ✗ Publication {PublicationCode} not found or not in category '{CategoryName}'", 
                    publicationCodeForTest, categoryName);
                allPassed = false;
            }
        }
        else
        {
            logger.Warning("    ✗ Failed to harvest publication {PublicationCode} for language {LanguageCode}", 
                publicationCodeForTest, languageCodeForTest);
            allPassed = false;
        }

        // Test 9: Scenario 6 - Section change cascade
        // This validates: when user selects a section, gets first track
        categoryName = "Bible";
        logger.Information("  Test 9: Scenario 6 - Section change cascade (selecting section 1 for 'nwt' in language 'E')");
        publicationCodeForTest = "nwt";
        languageCodeForTest = "E";
        var sectionNumberForTest = 1;

        // Step 1: Ensure publication and sections exist
        var pubHarvestSuccess2 = await languageContentService.EnsurePublicationExistsAsync(
            publicationCodeForTest, languageCodeForTest);

        if (pubHarvestSuccess2)
        {
            // Step 2: Get tracks for section using same logic as MediaService (same as HandleSectionCascadeAsync does)
            var tracks = await GetBiblePublicationTracksAsync(languageCodeForTest, publicationCodeForTest, sectionNumberForTest);
            
            if (tracks != null && tracks.Count > 0)
            {
                var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                logger.Information("    ✓ Found {Count} tracks for section {SectionNumber}", tracks.Count, sectionNumberForTest);
                logger.Information("    ✓ First track: Number={Number}, Title={Title}", 
                    firstTrack.Number, firstTrack.Title);
                
                // Verification: Check database to confirm tracks were saved
                var tracksInDb = await dbContext.BiblePublicationTracks
                    .AsNoTracking()
                    .Include(t => t.Section)
                        .ThenInclude(s => s.BiblePublication)
                            .ThenInclude(p => p.Language)
                    .Include(t => t.Section)
                        .ThenInclude(s => s.BiblePublication)
                            .ThenInclude(p => p.Category)
                    .Where(t => t.Section != null &&
                               t.Section.BiblePublication != null &&
                               t.Section.BiblePublication.PublicationCode == publicationCodeForTest &&
                               t.Section.BiblePublication.Language != null &&
                               t.Section.BiblePublication.Language.LanguageCode == languageCodeForTest &&
                               t.Section.BiblePublication.Category != null &&
                               t.Section.BiblePublication.Category.CategoryName == categoryName &&
                               t.Section.SectionCode == sectionNumberForTest.ToString())
                    .CountAsync();
                
                if (tracksInDb > 0)
                {
                    logger.Information("    ✓ Verified: {TrackCount} tracks saved in database", tracksInDb);
                }
            }
            else
            {
                logger.Warning("    ✗ No tracks found for section {SectionNumber} (category: {CategoryName})", 
                    sectionNumberForTest, categoryName);
                allPassed = false;
            }
        }
        else
        {
            logger.Warning("    ✗ Failed to harvest publication {PublicationCode} for language {LanguageCode}", 
                publicationCodeForTest, languageCodeForTest);
            allPassed = false;
        }

        // Test 10: Scenario 7 - Language change with existing publication
        // This validates: when user changes language but publication is already selected, harvests that publication for new language
        categoryName = "Bible";
        logger.Information("  Test 10: Scenario 7 - Language change with existing publication (changing from 'E' to 'S' for 'nwt')");
        publicationCodeForTest = "nwt";
        var oldLanguageCode = "E";
        var newLanguageCode = "S";

        // Step 1: Verify publication exists for old language
        var publicationExistsForOldLang = await dbContext.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Language)
            .Include(bp => bp.Category)
            .AnyAsync(bp => bp.PublicationCode == publicationCodeForTest &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == oldLanguageCode &&
                          bp.Category != null &&
                          bp.Category.CategoryName == categoryName);

        if (publicationExistsForOldLang)
        {
            logger.Information("    ✓ Publication {PublicationCode} exists for language {LanguageCode} (category: {CategoryName})", 
                publicationCodeForTest, oldLanguageCode, categoryName);

            // Step 2: Check if publication exists for new language
            var publicationExistsForNewLang = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForTest &&
                              bp.Language != null &&
                              bp.Language.LanguageCode == newLanguageCode &&
                              bp.Category != null &&
                              bp.Category.CategoryName == categoryName);

            logger.Information("    Publication exists for new language {LanguageCode} before harvest: {Exists}", 
                newLanguageCode, publicationExistsForNewLang);

            // Step 3: Harvest publication for new language (same as HandleLanguageCascadeAsync does when publication already selected)
            var harvestSuccessForNewLang = await languageContentService.EnsurePublicationExistsAsync(
                publicationCodeForTest, newLanguageCode);

            if (harvestSuccessForNewLang)
            {
                // Step 4: Verify publication now exists for new language
                var publicationForNewLang = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                    newLanguageCode, publicationCodeForTest);

                if (publicationForNewLang != null && publicationForNewLang.Category != null &&
                    publicationForNewLang.Category.CategoryName == categoryName)
                {
                    logger.Information("    ✓ Publication {PublicationCode} successfully harvested for language {LanguageCode} (category: {CategoryName})", 
                        publicationCodeForTest, newLanguageCode, categoryName);

                    // Step 5: Get first section and track for new language
                    if (publicationForNewLang.Sections != null && publicationForNewLang.Sections.Count > 0)
                    {
                        var firstSection = publicationForNewLang.Sections.OrderBy(s => s.SectionCode).First();
                        logger.Information("    ✓ First section for new language: Code={Code}, Name={Name}", 
                            firstSection.SectionCode, firstSection.Name);

                        // Get tracks for first section using same logic as MediaService
                        var sectionNumber = int.TryParse(firstSection.SectionCode, out var num) ? num : 1;
                        // First try to get tracks from database
                        var tracks = await GetBiblePublicationTracksAsync(newLanguageCode, publicationCodeForTest, sectionNumber);
                        
                        // If no tracks found, explicitly fetch them (matching cascade behavior)
                        if ((tracks == null || tracks.Count == 0) && languageContentService != null)
                        {
                            var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                                publicationCodeForTest, firstSection.SectionCode, newLanguageCode);
                            
                            if (fetchSuccess)
                            {
                                // Re-query tracks after fetching
                                tracks = await GetBiblePublicationTracksAsync(newLanguageCode, publicationCodeForTest, sectionNumber);
                            }
                        }
                        
                        if (tracks != null && tracks.Count > 0)
                        {
                            var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                            logger.Information("    ✓ First section has {TrackCount} tracks for new language", tracks.Count);
                            logger.Information("    ✓ First track: Number={Number}, Title={Title}", 
                                firstTrack.Number, firstTrack.Title);
                            
                            // Verification: Check database to confirm tracks were saved
                            var tracksInDb = await dbContext.BiblePublicationTracks
                                .AsNoTracking()
                                .Include(t => t.Section)
                                    .ThenInclude(s => s.BiblePublication)
                                        .ThenInclude(p => p.Language)
                                .Include(t => t.Section)
                                    .ThenInclude(s => s.BiblePublication)
                                        .ThenInclude(p => p.Category)
                                .Where(t => t.Section != null &&
                                           t.Section.BiblePublication != null &&
                                           t.Section.BiblePublication.PublicationCode == publicationCodeForTest &&
                                           t.Section.BiblePublication.Language != null &&
                                           t.Section.BiblePublication.Language.LanguageCode == newLanguageCode &&
                                           t.Section.BiblePublication.Category != null &&
                                           t.Section.BiblePublication.Category.CategoryName == categoryName &&
                                           t.Section.SectionCode == firstSection.SectionCode)
                                .CountAsync();
                            
                            if (tracksInDb > 0)
                            {
                                logger.Information("    ✓ Verified: {TrackCount} tracks saved in database", tracksInDb);
                            }
                        }
                        else
                        {
                            logger.Warning("    ✗ First section has no tracks for new language");
                            allPassed = false;
                        }
                    }
                    else
                    {
                        logger.Warning("    ✗ Publication has no sections for new language");
                        allPassed = false;
                    }
                }
                else
                {
                    logger.Warning("    ✗ Publication {PublicationCode} not found or not in category '{CategoryName}' for new language {LanguageCode}", 
                        publicationCodeForTest, categoryName, newLanguageCode);
                    allPassed = false;
                }
            }
            else
            {
                logger.Warning("    ✗ Failed to harvest publication {PublicationCode} for new language {LanguageCode}", 
                    publicationCodeForTest, newLanguageCode);
                allPassed = false;
            }
        }
        else
        {
            logger.Warning("    ✗ Publication {PublicationCode} does not exist for language {LanguageCode} (category: {CategoryName})", 
                publicationCodeForTest, oldLanguageCode, categoryName);
            allPassed = false;
        }

        return allPassed;
    }
}
