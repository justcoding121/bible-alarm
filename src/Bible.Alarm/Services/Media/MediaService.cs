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
        
        // First, try to get publications from database
        var publications = await BiblePublicationService.GetByLanguageCodeAsync(languageCode, categoryName, cancellationTokenSource.Token);
        
        // If no publications found and language is not English (E), fetch first publication with first section + tracks
        // English publications are pre-harvested by the harvester
        if (publications.Count == 0 && !string.IsNullOrEmpty(languageCode) && 
            !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("No publications found for language {LanguageCode}, fetching first publication with first section", languageCode);
            
            try
            {
                // Fetch first publication with first section + tracks
                var fetchSuccess = await languageContentService.FetchFirstPublicationForLanguageAsync(
                    languageCode, categoryName, cancellationTokenSource.Token);
                
                if (fetchSuccess)
                {
                    // Re-query database to get the fetched publication
                    publications = await BiblePublicationService.GetByLanguageCodeAsync(
                        languageCode, categoryName, cancellationTokenSource.Token);
                    
                    Log.Information("Successfully fetched and loaded {Count} publications for language {LanguageCode}", 
                        publications.Count, languageCode);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch first publication for language {LanguageCode}", languageCode);
            }
        }
        else if (publications.Count > 0 && !string.IsNullOrEmpty(languageCode) && 
                 !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            // Publications exist - ensure all publications for this language are downloaded
            // This is called when publication modal opens
            Log.Information("Ensuring all publications are downloaded for language {LanguageCode}", languageCode);
            
            try
            {
                await languageContentService.EnsureAllPublicationsForLanguageAsync(
                    languageCode, categoryName, cancellationTokenSource.Token);
                
                // Re-query to get any newly fetched publications
                publications = await BiblePublicationService.GetByLanguageCodeAsync(
                    languageCode, categoryName, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to ensure all publications for language {LanguageCode}", languageCode);
            }
        }
        
        return publications;
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
        
        // First, try to get vocal music releases from database
        var releases = await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
        
        // If no releases found and language is not English (E), fetch first release with first section + tracks
        // English vocal music is pre-harvested by the harvester
        if (releases.Count == 0 && !string.IsNullOrEmpty(languageCode) && 
            !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("No vocal music releases found for language {LanguageCode}, fetching first release with first section", languageCode);
            
            try
            {
                // Fetch first publication with first section + tracks (Music category)
                var fetchSuccess = await languageContentService.FetchFirstPublicationForLanguageAsync(
                    languageCode, "Music", cancellationTokenSource.Token);
                
                if (fetchSuccess)
                {
                    // Re-query database to get the fetched release
                    releases = await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
                    
                    Log.Information("Successfully fetched and loaded {Count} vocal music releases for language {LanguageCode}", 
                        releases.Count, languageCode);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch first vocal music release for language {LanguageCode}", languageCode);
            }
        }
        else if (releases.Count > 0 && !string.IsNullOrEmpty(languageCode) && 
                 !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            // Releases exist - ensure all publications for this language are downloaded
            // This is called when publication modal opens
            Log.Information("Ensuring all vocal music releases are downloaded for language {LanguageCode}", languageCode);
            
            try
            {
                await languageContentService.EnsureAllPublicationsForLanguageAsync(
                    languageCode, "Music", cancellationTokenSource.Token);
                
                // Re-query to get any newly fetched releases
                releases = await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to ensure all vocal music releases for language {LanguageCode}", languageCode);
            }
        }
        
        return releases;
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
