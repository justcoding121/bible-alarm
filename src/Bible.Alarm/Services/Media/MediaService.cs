#nullable enable annotations
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.MediaServiceHelpers;
using Bible.Alarm.Shared.Constants;
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
using System.Collections.Concurrent;

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

    private static readonly TimeSpan BiblePublicationsCacheTtl = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<BiblePublicationsCacheKey, BiblePublicationsCacheEntry> biblePublicationsCache = new();
    private readonly ConcurrentDictionary<string, bool> publicationWithoutLanguageCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct BiblePublicationsCacheKey(string LanguageCode, string? CategoryName, bool RequireIsMusicForMusicCategory);

    private sealed class BiblePublicationsCacheEntry(DateTimeOffset createdAt, Lazy<Task<Dictionary<string, BiblePublication>>> value)
    {
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public Lazy<Task<Dictionary<string, BiblePublication>>> Value { get; } = value;
    }

    public async Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false)
    {
        await mediaIndexService.Verify();
        return await BiblePublicationService.GetDistinctLanguagesAsync(categoryName, requireIsMusicForMusicCategory, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false)
    {
        // Cache only the "read-only" variant used by UI display/selectability checks.
        // If downloadAll=true or progress is provided, we must execute fresh to support downloads/progress reporting.
        if (downloadAll || progress != null)
        {
            await mediaIndexService.Verify();
            // Use cancellation token from progress tracker if available, otherwise use MediaService's token
            // This allows cancellation from the UI (e.g., cancel button) to propagate through the call chain
            var cancellationToken = progress?.CancellationToken ?? cancellationTokenSource.Token;
            var result = await MediaServiceBiblePublicationList.GetBiblePublicationsAsync(
                BiblePublicationService,
                languageContentService,
                scopeFactory,
                cancellationToken,
                languageCode,
                categoryName,
                downloadAll,
                progress,
                requireIsMusicForMusicCategory);
            
            // Invalidate cache after downloading to ensure selectability checks use fresh data
            if (downloadAll)
            {
                InvalidateBiblePublicationsCache(languageCode, categoryName);
            }
            
            return result;
        }

        var normalizedLanguage = languageCode ?? string.Empty;
        var normalizedCategory = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        var requireIsMusic = requireIsMusicForMusicCategory && string.Equals(normalizedCategory, "Music", StringComparison.OrdinalIgnoreCase);
        var key = new BiblePublicationsCacheKey(normalizedLanguage, normalizedCategory, requireIsMusic);
        var now = DateTimeOffset.UtcNow;

        static Lazy<Task<Dictionary<string, BiblePublication>>> CreateLazy(
            MediaService self,
            string lang,
            string? cat,
            bool requireIsMusicForMusicCategory)
            => new(() => self.LoadBiblePublicationsUncachedAsync(lang, cat, requireIsMusicForMusicCategory),
                LazyThreadSafetyMode.ExecutionAndPublication);

        var entry = biblePublicationsCache.AddOrUpdate(
            key,
            _ => new BiblePublicationsCacheEntry(now, CreateLazy(this, normalizedLanguage, normalizedCategory, requireIsMusic)),
            (_, existing) =>
                now - existing.CreatedAt <= BiblePublicationsCacheTtl
                    ? existing
                    : new BiblePublicationsCacheEntry(now, CreateLazy(this, normalizedLanguage, normalizedCategory, requireIsMusic)));

        try
        {
            return await entry.Value.Value;
        }
        catch
        {
            // If the cached task fails, remove it so next call can retry.
            biblePublicationsCache.TryRemove(key, out _);
            throw;
        }
    }

    private async Task<Dictionary<string, BiblePublication>> LoadBiblePublicationsUncachedAsync(string languageCode, string? categoryName, bool requireIsMusicForMusicCategory = false)
    {
        await mediaIndexService.Verify();
        return await MediaServiceBiblePublicationList.GetBiblePublicationsAsync(
            BiblePublicationService,
            languageContentService,
            scopeFactory,
            cancellationTokenSource.Token,
            languageCode,
            categoryName,
            downloadAll: false,
            progress: null,
            requireIsMusicForMusicCategory);
    }

    /// <summary>
    /// Invalidates the BiblePublications cache for a specific language and category.
    /// Call this after downloading publications to ensure fresh data is used for selectability checks.
    /// </summary>
    public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
    {
        var normalizedLanguage = languageCode ?? string.Empty;
        var normalizedCategory = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        biblePublicationsCache.TryRemove(new BiblePublicationsCacheKey(normalizedLanguage, normalizedCategory, false), out _);
        if (string.Equals(normalizedCategory, "Music", StringComparison.OrdinalIgnoreCase))
        {
            biblePublicationsCache.TryRemove(new BiblePublicationsCacheKey(normalizedLanguage, normalizedCategory, true), out _);
        }
    }

    public async Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return false;
        }

        if (publicationWithoutLanguageCache.TryGetValue(publicationCode, out var cached))
        {
            return cached;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var publicationWithoutLanguage = await db.BiblePublications
            .AsNoTracking()
            .AnyAsync(bp => bp.PublicationCode == publicationCode && bp.LanguageId == null, cancellationTokenSource.Token);

        publicationWithoutLanguageCache[publicationCode] = publicationWithoutLanguage;
        return publicationWithoutLanguage;
    }

    public async Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(
        string languageCode, string versionCode, IFetchProgress? progress = null)
    {
        await mediaIndexService.Verify();
        
        // Check if publication has LanguageId == null (publications without language)
        // If so, use GetSectionsForPublicationWithoutLanguage which handles publications without language
        if (await IsPublicationWithoutLanguageAsync(versionCode))
        {
            // Publication has LanguageId == null - use GetSectionsForPublicationWithoutLanguage which handles this case
            Log.Debug("Publication {PublicationCode} has LanguageId == null, using GetSectionsForPublicationWithoutLanguage", versionCode);
            return await GetSectionsForPublicationWithoutLanguage(versionCode);
        }
        
        // Publication has a language - use standard query
        // First, try to get sections from database
        var sections = await biblePublicationSectionService.GetSectionsByPublicationAsync(
            languageCode, versionCode, cancellationTokenSource.Token);
        
        // IMPORTANT:
        // Fetching "all sections" can be expensive (network + DB writes) and should only happen
        // when the user explicitly opens the Sections modal (where we can show progress).
        // Other callers (playback navigation, playlist building, etc.) should remain read-only.
        //
        // English sections are pre-cataloged by the cataloger.
        if (!string.IsNullOrEmpty(languageCode) &&
            !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase) &&
            progress != null)
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
            catch (System.Net.Http.HttpRequestException)
            {
                throw;
            }
            catch (System.Net.Sockets.SocketException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to ensure all sections for publication {PublicationCode} in language {LanguageCode}", 
                    versionCode, languageCode);
            }
        }
        
        return sections;
    }

    public async Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode)
    {
        await mediaIndexService.Verify();
        
        // First, try to get sections from database
        var sections = await biblePublicationSectionService.GetSectionsByPublicationWithoutLanguageAsync(publicationCode, cancellationTokenSource.Token);
        
        // If no sections found, the publication might not be cataloged yet
        // For publications without language (like "iam"), sections should be pre-cataloged
        // But if they're not, we can't catalog them here (no language code to use)
        // The user should run the cataloger to pre-catalog these publications
        if (sections == null || sections.Count == 0)
        {
            Log.Warning("GetSectionsForPublicationWithoutLanguage: No sections found for publication {PublicationCode}. " +
                "This publication may not be cataloged yet. Publications without language (like 'iam') should be pre-cataloged.",
                publicationCode);
        }
        
        return sections ?? new SortedDictionary<string, BiblePublicationSection>(SectionCodeHelper.SectionCodeComparer);
    }

    public async Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode)
    {
        await mediaIndexService.Verify();
        return await biblePublicationSectionService.GetSectionAsync(languageCode, versionCode, sectionCode, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<string, BiblePublicationTrack>>
        GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode)
    {
        await mediaIndexService.Verify();
        
        // Check if publication has LanguageId == null (publications without language)
        // If so, query tracks directly from database without language code
        if (await IsPublicationWithoutLanguageAsync(versionCode))
        {
            // Publication has LanguageId == null - query tracks directly from database
            Log.Debug("Publication {PublicationCode} has LanguageId == null, querying tracks directly", versionCode);
            return await GetTracksForPublicationWithoutLanguage(versionCode, sectionCode);
        }
        
        // Publication has a language - use standard query
        return await biblePublicationTrackService.GetTracksBySectionAsync(languageCode, versionCode, sectionCode, cancellationTokenSource.Token);
    }
    
    private Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksForPublicationWithoutLanguage(
        string publicationCode, string? sectionCode) =>
        MediaServiceTracksForNoLanguageHelper.GetTracksAsync(
            scopeFactory,
            publicationCode,
            sectionCode,
            cancellationTokenSource.Token);

    public async Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode,
        string versionCode, string? sectionCode, string trackCode)
    {
        await mediaIndexService.Verify();
        return await biblePublicationTrackService.GetTrackAsync(languageCode, versionCode, sectionCode, trackCode, cancellationTokenSource.Token);
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
        
        // Avoid N+1 queries in VocalMusicService.GetDistinctLanguagesAsync.
        // PublicationLanguages already has the discovery data we need for Music languages.
        // This is cached inside BiblePublicationService, so repeated calls are cheap.
        var result = await BiblePublicationService.GetDistinctLanguagesAsync("Music", true, cancellationTokenSource.Token);
        Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: returned {Count} languages", result.Count);

        // If no vocal languages found, fall back to basic languages (English)
        // This can happen if the vocal music database doesn't have language metadata
        if (result.Count == 0)
        {
            Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: No vocal languages found, falling back to English");
            result = new Dictionary<string, Language>
            {
                ["E"] = new Language { LanguageCode = "E" }
            };
        }

        return result;
    }

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false)
    {
        await mediaIndexService.Verify();
        var result = await MediaServiceVocalMusicHelper.GetReleasesAsync(
            BiblePublicationService,
            vocalMusicService,
            scopeFactory,
            languageCode,
            cancellationTokenSource.Token);
        if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
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
        string? sectionCode, string trackCode, string url)
    {
        await mediaIndexService.Verify();
        await biblePublicationTrackService.UpdateTrackUrlAsync(languageCode, versionCode, sectionCode, trackCode, url, cancellationTokenSource.Token);
    }

    public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
        string trackCode, string url)
    {
        await mediaIndexService.Verify();
        await vocalMusicService.UpdateTrackUrlAsync(languageCode, publicationCode, trackCode, url, cancellationTokenSource.Token);
    }

    public async Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url)
    {
        await mediaIndexService.Verify();
        await melodyMusicService.UpdateTrackUrlAsync(publicationCode, trackCode, url, cancellationTokenSource.Token);
    }

    public async Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url)
    {
        if (trackMetadata.PlayType == PlayType.Bible)
        {
            await UpdateBiblePublicationTrackUrl(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.SectionCode,
                trackMetadata.TrackCode,
                url);
        }
        else
        {
            if (trackMetadata.LanguageCode == null)
            {
                await UpdateMelodyTrackUrl(
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackCode,
                    url);
            }
            else
            {
                await UpdateVocalTrackUrl(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackCode,
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

    public async Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode)
    {
        await mediaIndexService.Verify();
        return await MediaServiceExpectedCountHelper.GetExpectedSectionCountAsync(
            scopeFactory,
            languageCode,
            publicationCode,
            cancellationTokenSource.Token);
    }

    public async Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false)
    {
        await mediaIndexService.Verify();
        return await MediaServiceExpectedCountHelper.GetExpectedPublicationCountAsync(
            scopeFactory,
            languageCode,
            categoryName,
            cancellationTokenSource.Token,
            requireIsMusicForMusicCategory);
    }

    public async Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode)
    {
        await mediaIndexService.Verify();
        return await MediaServiceExpectedCountHelper.GetExpectedSectionCountForNoLanguagePublicationAsync(
            scopeFactory,
            publicationCode,
            cancellationTokenSource.Token);
    }
}
