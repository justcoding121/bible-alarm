using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaService(
    IMediaIndexService mediaIndexService,
    IBiblePublicationService BiblePublicationService,
    IBiblePublicationSectionService biblePublicationSectionService,
    IBiblePublicationTrackService biblePublicationTrackService,
    IMelodyMusicService melodyMusicService,
    IVocalMusicService vocalMusicService,
    ILanguageContentService languageContentService,
    IServiceScopeFactory scopeFactory)
    : IMediaService, IDisposable
{
    private readonly IMediaIndexService mediaIndexService = mediaIndexService ?? throw new ArgumentNullException(nameof(mediaIndexService));
    private readonly IBiblePublicationService BiblePublicationService = BiblePublicationService ?? throw new ArgumentNullException(nameof(BiblePublicationService));
    private readonly IBiblePublicationSectionService biblePublicationSectionService = biblePublicationSectionService ?? throw new ArgumentNullException(nameof(biblePublicationSectionService));
    private readonly IBiblePublicationTrackService biblePublicationTrackService = biblePublicationTrackService ?? throw new ArgumentNullException(nameof(biblePublicationTrackService));
    private readonly IMelodyMusicService melodyMusicService = melodyMusicService ?? throw new ArgumentNullException(nameof(melodyMusicService));
    private readonly IVocalMusicService vocalMusicService = vocalMusicService ?? throw new ArgumentNullException(nameof(vocalMusicService));
    private readonly ILanguageContentService languageContentService = languageContentService ?? throw new ArgumentNullException(nameof(languageContentService));
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    // Priority codes for Bible publications: nwt (2013 NWT), bi12 (1984 NWT)
    private static readonly string[] PriorityPublicationCodes = ["nwt", "bi12"];

    public async Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null)
    {
        await mediaIndexService.Verify();
        return await BiblePublicationService.GetDistinctLanguagesAsync(categoryName, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null)
    {
        await mediaIndexService.Verify();
        
        // Step 1: Get all available publication codes from PublicationLanguages (discovery table)
        // This shows all publications that are available for this language/category, even if not yet downloaded
        var availablePublicationCodes = await BiblePublicationService.GetAvailablePublicationCodesAsync(
            languageCode, categoryName, cancellationTokenSource.Token);
        
        Log.Debug("GetBiblePublications: Found {Count} available publication codes from PublicationLanguages for language={LanguageCode}, category={CategoryName}",
            availablePublicationCodes.Count, languageCode, categoryName ?? "all");
        
        // Step 2: Get downloaded publications from BiblePublications table
        var downloadedPublications = await BiblePublicationService.GetByLanguageCodeAsync(
            languageCode, categoryName, cancellationTokenSource.Token);
        
        Log.Debug("GetBiblePublications: Found {Count} downloaded publications for language={LanguageCode}, category={CategoryName}",
            downloadedPublications.Count, languageCode, categoryName ?? "all");
        
        // Step 3: Merge - use downloaded publications where available, create placeholders for others
        var result = new Dictionary<string, BiblePublication>();
        
        // Add downloaded publications
        foreach (var downloadedPub in downloadedPublications.Values)
        {
            result[downloadedPub.PublicationCode] = downloadedPub;
        }
        
        // Create placeholders for publications that are available but not yet downloaded
        // We need to get Category and Language from PublicationLanguages to create proper placeholders
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var missingPublicationCodes = availablePublicationCodes
            .Where(code => !result.ContainsKey(code))
            .ToList();
        
        if (missingPublicationCodes.Count > 0)
        {
            Log.Debug("GetBiblePublications: Creating placeholders for {Count} publications not yet downloaded", missingPublicationCodes.Count);
            
            // Get Category and Language info from PublicationLanguages for missing publications
            var publicationLanguageInfo = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode &&
                             missingPublicationCodes.Contains(pl.PublicationCode))
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryName == categoryName))
                .ToListAsync(cancellationTokenSource.Token);
            
            foreach (var plInfo in publicationLanguageInfo)
            {
                if (plInfo.Category == null || plInfo.Language == null)
                    continue;
                
                // Create placeholder BiblePublication
                var placeholder = new BiblePublication
                {
                    Id = 0, // Not saved yet
                    PublicationCode = plInfo.PublicationCode,
                    Name = plInfo.PublicationCode, // Placeholder name - will be updated when downloaded
                    CategoryId = plInfo.CategoryId,
                    Category = plInfo.Category,
                    LanguageId = plInfo.LanguageId,
                    Language = plInfo.Language,
                    Sections = new List<BiblePublicationSection>(),
                    Tracks = new List<BiblePublicationTrack>(),
                    IsVideo = false // Will be set correctly when downloaded
                };
                
                result[placeholder.PublicationCode] = placeholder;
            }
        }
        
        Log.Information("GetBiblePublications: Returning {TotalCount} publications ({DownloadedCount} downloaded, {PlaceholderCount} placeholders) for language={LanguageCode}, category={CategoryName}",
            result.Count, downloadedPublications.Count, result.Count - downloadedPublications.Count, languageCode, categoryName ?? "all");
        
        // Step 4: If language is not English and publications modal is opened, ensure all are downloaded in background
        // (This happens when user opens the publication modal)
        if (!string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            // Fire and forget - don't block the UI
            _ = Task.Run(async () =>
            {
                try
                {
                    Log.Information("Background: Ensuring all publications are downloaded for language {LanguageCode}", languageCode);
                    await languageContentService.EnsureAllPublicationsForLanguageAsync(
                        languageCode, categoryName, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Background: Failed to ensure all publications for language {LanguageCode}", languageCode);
                }
            });
        }
        
        return result;
    }
    
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

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(
        string languageCode, string versionCode)
    {
        await mediaIndexService.Verify();
        
        // First, try to get sections from database
        var sections = await biblePublicationSectionService.GetSectionsByPublicationAsync(
            languageCode, versionCode, cancellationTokenSource.Token);
        
        // If language is not English (E), ensure all sections are downloaded
        // This is called when sections modal opens
        // English sections are pre-harvested by the harvester
        if (!string.IsNullOrEmpty(languageCode) && 
            !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("Ensuring all sections are downloaded for publication {PublicationCode} in language {LanguageCode}", 
                versionCode, languageCode);
            
            try
            {
                // Ensure all sections are downloaded (without tracks - tracks are fetched when section is selected)
                var fetchSuccess = await languageContentService.EnsureAllSectionsForPublicationAsync(
                    versionCode, languageCode, cancellationTokenSource.Token);
                
                if (fetchSuccess)
                {
                    // Re-query database to get all sections (including newly fetched ones)
                    sections = await biblePublicationSectionService.GetSectionsByPublicationAsync(
                        languageCode, versionCode, cancellationTokenSource.Token);
                    
                    Log.Information("Successfully loaded {Count} sections for publication {PublicationCode} in language {LanguageCode}", 
                        sections.Count, versionCode, languageCode);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to ensure all sections for publication {PublicationCode} in language {LanguageCode}", 
                    versionCode, languageCode);
            }
        }
        
        return sections;
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetMusicSections(string publicationCode)
    {
        await mediaIndexService.Verify();
        
        // First, try to get sections from database
        var sections = await biblePublicationSectionService.GetMusicSectionsByPublicationAsync(publicationCode, cancellationTokenSource.Token);
        
        // For instrumental music (iam), sections are pre-harvested (language is null)
        // For vocal music, we need to check if all sections are downloaded
        // Note: Music sections don't have a language code, so we can't use EnsureAllSectionsForPublicationAsync directly
        // Instead, we check if sections exist and fetch if needed
        // TODO: Add method to ensure all music sections are downloaded if needed
        
        return sections;
    }

    public async Task<BiblePublicationSection> GetBiblePublicationSection(string languageCode, string versionCode, int sectionNumber)
    {
        await mediaIndexService.Verify();
        return await biblePublicationSectionService.GetSectionAsync(languageCode, versionCode, sectionNumber, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, BiblePublicationTrack>>
        GetBiblePublicationTracks(string languageCode, string versionCode, int sectionNumber)
    {
        await mediaIndexService.Verify();
        return await biblePublicationTrackService.GetTracksBySectionAsync(languageCode, versionCode, sectionNumber, cancellationTokenSource.Token);
    }

    public async Task<BiblePublicationTrack> GetBiblePublicationTrack(string languageCode,
        string versionCode, int sectionNumber, int trackNumber)
    {
        await mediaIndexService.Verify();
        return await biblePublicationTrackService.GetTrackAsync(languageCode, versionCode, sectionNumber, trackNumber, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases()
    {
        await mediaIndexService.Verify();
        return await melodyMusicService.GetAllAsync(cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetMelodyMusicTracks(string publicationCode)
    {
        await mediaIndexService.Verify();
        return await melodyMusicService.GetTracksByCodeAsync(publicationCode, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode)
    {
        await mediaIndexService.Verify();
        return await melodyMusicService.GetTracksBySectionCodeAsync(publicationCode, sectionCode, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        await mediaIndexService.Verify();
        var result = await vocalMusicService.GetDistinctLanguagesAsync(cancellationTokenSource.Token);
        Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: returned {Count} languages: {LanguageCodes}",
            result.Count, string.Join(", ", result.Keys));

        // If no vocal languages found, fall back to basic languages (English)
        // This can happen if the vocal music database doesn't have language metadata
        if (result.Count == 0)
        {
            Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: No vocal languages found, falling back to English");
            result = new Dictionary<string, Language>
            {
                ["E"] = new Language { LanguageCode = "E", Name = "English" }
            };
        }

        return result;
    }

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode)
    {
        await mediaIndexService.Verify();
        
        // Step 1: Get all available publication codes from PublicationLanguages for Music category (discovery table)
        // This shows all vocal music publications that are available for this language, even if not yet downloaded
        // Note: We filter for publications that have LanguageId (vocal music, not instrumental "iam")
        var availablePublicationCodes = await BiblePublicationService.GetAvailablePublicationCodesAsync(
            languageCode, "Music", cancellationTokenSource.Token);
        
        // Filter out "iam" (Kingdom Melodies) as it's instrumental music (LanguageId = null)
        var vocalPublicationCodes = availablePublicationCodes
            .Where(code => !code.Equals("iam", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        Log.Debug("GetVocalMusicReleases: Found {Count} available vocal music publication codes from PublicationLanguages for language={LanguageCode}",
            vocalPublicationCodes.Count, languageCode);
        
        // Step 2: Get downloaded vocal music releases from BiblePublications table
        var downloadedReleases = await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
        
        Log.Debug("GetVocalMusicReleases: Found {Count} downloaded vocal music releases for language={LanguageCode}",
            downloadedReleases.Count, languageCode);
        
        // Step 3: Merge - use downloaded releases where available, create placeholders for others
        var result = new Dictionary<string, VocalMusic>();
        
        // Add downloaded releases
        foreach (var downloadedRelease in downloadedReleases.Values)
        {
            result[downloadedRelease.Code] = downloadedRelease;
        }
        
        // Create placeholders for publications that are available but not yet downloaded
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var missingPublicationCodes = vocalPublicationCodes
            .Where(code => !result.ContainsKey(code))
            .ToList();
        
        if (missingPublicationCodes.Count > 0)
        {
            Log.Debug("GetVocalMusicReleases: Creating placeholders for {Count} vocal music releases not yet downloaded", missingPublicationCodes.Count);
            
            // Get Category and Language info from PublicationLanguages for missing publications
            var publicationLanguageInfo = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode &&
                             pl.Category != null && pl.Category.CategoryName == "Music" &&
                             missingPublicationCodes.Contains(pl.PublicationCode))
                .ToListAsync(cancellationTokenSource.Token);
            
            foreach (var plInfo in publicationLanguageInfo)
            {
                if (plInfo.Category == null || plInfo.Language == null)
                    continue;
                
                // Create placeholder BiblePublication for vocal music
                var placeholderPublication = new BiblePublication
                {
                    Id = 0, // Not saved yet
                    PublicationCode = plInfo.PublicationCode,
                    Name = plInfo.PublicationCode, // Placeholder name - will be updated when downloaded
                    CategoryId = plInfo.CategoryId,
                    Category = plInfo.Category,
                    LanguageId = plInfo.LanguageId,
                    Language = plInfo.Language,
                    Sections = new List<BiblePublicationSection>(),
                    Tracks = new List<BiblePublicationTrack>(),
                    IsVideo = false
                };
                
                var placeholder = new VocalMusic { Publication = placeholderPublication };
                result[placeholder.Code] = placeholder;
            }
        }
        
        Log.Information("GetVocalMusicReleases: Returning {TotalCount} vocal music releases ({DownloadedCount} downloaded, {PlaceholderCount} placeholders) for language={LanguageCode}",
            result.Count, downloadedReleases.Count, result.Count - downloadedReleases.Count, languageCode);
        
        // Step 4: If language is not English and publications modal is opened, ensure all are downloaded in background
        // (This happens when user opens the publication modal)
        if (!string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            // Fire and forget - don't block the UI
            _ = Task.Run(async () =>
            {
                try
                {
                    Log.Information("Background: Ensuring all vocal music releases are downloaded for language {LanguageCode}", languageCode);
                    await languageContentService.EnsureAllPublicationsForLanguageAsync(
                        languageCode, "Music", cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Background: Failed to ensure all vocal music releases for language {LanguageCode}", languageCode);
                }
            });
        }
        
        return result;
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        await mediaIndexService.Verify();
        return await vocalMusicService.GetTracksByLanguageAndCodeAsync(languageCode, publicationCode, cancellationTokenSource.Token);
    }

    public async Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode,
        int sectionNumber, int trackNumber, string url)
    {
        await mediaIndexService.Verify();
        await biblePublicationTrackService.UpdateTrackUrlAsync(languageCode, versionCode, sectionNumber, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
        int trackNumber, string url)
    {
        await mediaIndexService.Verify();
        await vocalMusicService.UpdateTrackUrlAsync(languageCode, publicationCode, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url)
    {
        await mediaIndexService.Verify();
        await melodyMusicService.UpdateTrackUrlAsync(publicationCode, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url)
    {
        if (trackMetadata.PlayType == PlayType.Bible)
        {
            await UpdateBiblePublicationTrackUrl(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.SectionNumber,
                trackMetadata.TrackNumber,
                url);
        }
        else
        {
            if (trackMetadata.LanguageCode == null)
            {
                await UpdateMelodyTrackUrl(
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackNumber,
                    url);
            }
            else
            {
                await UpdateVocalTrackUrl(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackNumber,
                    url);
            }
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            Log.Logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // Note: DbContext is now created via IServiceScopeFactory and disposed by the scope
        // mediaIndexService (MediaIndexService) and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
