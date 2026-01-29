#nullable enable annotations
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

    // Using centralized sorting helper from Bible.Alarm.Shared.Helpers.PublicationSortHelper

    public async Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null)
    {
        await mediaIndexService.Verify();
        return await BiblePublicationService.GetDistinctLanguagesAsync(categoryName, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null)
    {
        await mediaIndexService.Verify();
        return await MediaServiceBiblePublicationList.GetBiblePublicationsAsync(
            BiblePublicationService,
            languageContentService,
            scopeFactory,
            cancellationTokenSource.Token,
            languageCode,
            categoryName,
            downloadAll,
            progress);
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(
        string languageCode, string versionCode, IFetchProgress? progress = null)
    {
        await mediaIndexService.Verify();
        
        // Check if publication has LanguageId == null (publications without language)
        // If so, use GetSectionsForPublicationWithoutLanguage which handles publications without language
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var publicationWithoutLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == versionCode && bp.LanguageId == null, cancellationTokenSource.Token);
            
            if (publicationWithoutLanguage)
            {
                // Publication has LanguageId == null - use GetSectionsForPublicationWithoutLanguage which handles this case
                Log.Debug("Publication {PublicationCode} has LanguageId == null, using GetSectionsForPublicationWithoutLanguage", versionCode);
                return await GetSectionsForPublicationWithoutLanguage(versionCode);
            }
        }
        
        // Publication has a language - use standard query
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
                    versionCode, languageCode, cancellationTokenSource.Token, progress);
                
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

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode)
    {
        await mediaIndexService.Verify();
        
        // First, try to get sections from database
        var sections = await biblePublicationSectionService.GetSectionsByPublicationWithoutLanguageAsync(publicationCode, cancellationTokenSource.Token);
        
        // If no sections found, the publication might not be harvested yet
        // For publications without language (like "iam"), sections should be pre-harvested
        // But if they're not, we can't harvest them here (no language code to use)
        // The user should run the harvester to pre-harvest these publications
        if (sections == null || sections.Count == 0)
        {
            Log.Warning("GetSectionsForPublicationWithoutLanguage: No sections found for publication {PublicationCode}. " +
                "This publication may not be harvested yet. Publications without language (like 'iam') should be pre-harvested.",
                publicationCode);
        }
        
        return sections ?? new SortedDictionary<int, BiblePublicationSection>();
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
        
        // Check if publication has LanguageId == null (publications without language)
        // If so, query tracks directly from database without language code
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var publicationWithoutLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == versionCode && bp.LanguageId == null, cancellationTokenSource.Token);
            
            if (publicationWithoutLanguage)
            {
                // Publication has LanguageId == null - query tracks directly from database
                Log.Debug("Publication {PublicationCode} has LanguageId == null, querying tracks directly", versionCode);
                return await GetTracksForPublicationWithoutLanguage(versionCode, sectionNumber);
            }
        }
        
        // Publication has a language - use standard query
        return await biblePublicationTrackService.GetTracksBySectionAsync(languageCode, versionCode, sectionNumber, cancellationTokenSource.Token);
    }
    
    private async Task<SortedDictionary<int, BiblePublicationTrack>> GetTracksForPublicationWithoutLanguage(
        string publicationCode, int sectionNumber)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        // Convert sectionNumber to sectionCode (for music, sectionCode might be like "iam-1")
        // First, try to find the section by number
        var publication = await db.BiblePublications
            .AsNoTracking()
            .Include(x => x.Sections)
                .ThenInclude(s => s.Tracks)
            .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
            .FirstOrDefaultAsync(cancellationTokenSource.Token);
        
        if (publication?.Sections == null)
        {
            return new SortedDictionary<int, BiblePublicationTrack>();
        }
        
        // Find section by sectionNumber (try both numeric and non-numeric section codes)
        BiblePublicationSection? section = null;
        
        // First, try to find by sectionNumber as string
        var sectionCodeString = sectionNumber.ToString();
        section = publication.Sections.FirstOrDefault(s => s.SectionCode.Equals(sectionCodeString, StringComparison.OrdinalIgnoreCase));
        
        // If not found, try to find by extracting number from sectionCode (e.g., "iam-1" -> 1)
        if (section == null)
        {
            section = publication.Sections.FirstOrDefault(s =>
            {
                if (int.TryParse(s.SectionCode, out var code))
                {
                    return code == sectionNumber;
                }
                
                // Try to extract number from sectionCode like "iam-1"
                var parts = s.SectionCode.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out var extractedNumber))
                {
                    return extractedNumber == sectionNumber;
                }
                
                return false;
            });
        }
        
        if (section?.Tracks == null || section.Tracks.Count == 0)
        {
            return new SortedDictionary<int, BiblePublicationTrack>();
        }
        
        var tracksDict = section.Tracks
            .OrderBy(t => t.Number)
            .ToDictionary(t => t.Number, t => t);
        
        return new SortedDictionary<int, BiblePublicationTrack>(tracksDict);
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
        Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: returned {Count} languages",
            result.Count);

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

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false)
    {
        await mediaIndexService.Verify();
        
        // Step 1: Get all available publication codes from PublicationLanguages for Music category (discovery table)
        // This shows all vocal music publications that are available for this language, even if not yet downloaded
        var availablePublicationCodes = await BiblePublicationService.GetAvailablePublicationCodesAsync(
            languageCode, "Music", cancellationTokenSource.Token);
        
        Log.Debug("GetVocalMusicReleases: Found {Count} available publication codes from PublicationLanguages for language={LanguageCode}",
            availablePublicationCodes.Count, languageCode);
        
        // Step 2: Get downloaded vocal music releases from BiblePublications table (with language)
        var downloadedReleases = await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
        
        // Also get publications without language FK (LanguageId == null) for Music category
        // This is data-driven - works for any category that has publications without language
        Dictionary<string, BiblePublication> publicationsWithoutLanguage = new();
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var pubsWithoutLang = await db.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.Category != null && 
                           x.Category.CategoryName == "Music" &&
                           x.LanguageId == null)
                .ToListAsync(cancellationTokenSource.Token);
            
            foreach (var pub in pubsWithoutLang)
            {
                publicationsWithoutLanguage[pub.PublicationCode] = pub;
            }
        }
        
        Log.Debug("GetVocalMusicReleases: Found {Count} downloaded vocal music releases for language={LanguageCode}, and {CountWithoutLang} publications without language FK",
            downloadedReleases.Count, languageCode, publicationsWithoutLanguage.Count);
        
        // Step 3: Merge - use downloaded releases where available, create placeholders for others
        var result = new Dictionary<string, VocalMusic>();
        
        // Add downloaded releases with language
        foreach (var downloadedRelease in downloadedReleases.Values)
        {
            result[downloadedRelease.Code] = downloadedRelease;
        }
        
        // Add downloaded publications without language FK (data-driven, not hard-coded)
        // Convert BiblePublication to VocalMusic for consistency
        foreach (var pubWithoutLang in publicationsWithoutLanguage.Values)
        {
            // Only add if not already in result (avoid duplicates)
            if (!result.ContainsKey(pubWithoutLang.PublicationCode))
            {
                var vocalMusic = new VocalMusic { Publication = pubWithoutLang };
                result[vocalMusic.Code] = vocalMusic;
            }
        }
        
        // Create placeholders for publications that are available but not yet downloaded
        using var scope2 = scopeFactory.CreateScope();
        var dbContext = scope2.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var missingPublicationCodes = availablePublicationCodes
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
        
        // Step 4: Download publications based on downloadAll flag
        // - If downloadAll=true (publication modal opened): download all publications with their first sections and tracks
        // - If downloadAll=false (language selected): don't download here (will be done in cascade)
        if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            // Fire and forget - don't block the UI
            _ = Task.Run(async () =>
            {
                try
                {
                    Log.Information("Background: Ensuring all vocal music releases are downloaded for language {LanguageCode} (publication modal opened)", languageCode);
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
