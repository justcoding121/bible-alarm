#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing VocalMusic database operations.
/// Uses BiblePublications table filtered by Music category with LanguageId (Vocals).
/// </summary>
public sealed class VocalMusicService(IServiceScopeFactory scopeFactory, ILogger logger) : IVocalMusicService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;
    private const string MusicCategoryName = "Music";

    public async Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Language)
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId != null 
                    && x.Language!.LanguageCode == languageCode 
                    && x.PublicationCode == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            // VocalMusic is a subclass of BiblePublication, so we can return the publication directly
            // The publication is already filtered to be Music category with LanguageId
            return publication != null ? new VocalMusic { Publication = publication } : null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting VocalMusic. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var vocalMusicList = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Language)
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId != null 
                    && x.Language!.LanguageCode == languageCode)
                .ToListAsync(cancellationToken);

            // Handle potential duplicates gracefully - use first occurrence
            var result = new Dictionary<string, VocalMusic>();
            foreach (var vm in vocalMusicList)
            {
                if (!result.ContainsKey(vm.PublicationCode))
                {
                    result[vm.PublicationCode] = new VocalMusic { Publication = vm };
                }
                else
                {
                    logger.Warning("Duplicate VocalMusic entry found. LanguageCode={LanguageCode}, Code={Code}", languageCode, vm.PublicationCode);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting VocalMusic by language code. LanguageCode={LanguageCode}", languageCode);
            throw;
        }
    }

    public async Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Use PublicationLanguage table for discovery - it's designed for this purpose
            // This table tracks which languages are available for each publication code in the Music category
            // Filter for Music category and publications that have LanguageId (vocal music, not instrumental)
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => x.Category != null && x.Category.CategoryName == MusicCategoryName && x.Language != null);

            // For vocal music, we need to check if the publication has LanguageId in BiblePublications
            // But since we're querying PublicationLanguages, we can't directly filter by LanguageId
            // Instead, we'll get all Music languages from PublicationLanguages, then filter out instrumental ones
            // Instrumental music (iam) has LanguageId = null, so we exclude it
            var publicationLanguagesCount = await query.CountAsync(cancellationToken);
            var distinctLanguages = await query
                .Select(x => x.Language!)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Filter out languages that only have instrumental music (iam) - check if any vocal music exists
            // We do this by checking if there's a BiblePublication with LanguageId for this language in Music category
            var vocalLanguages = new List<Language>();
            foreach (var lang in distinctLanguages)
            {
                var hasVocalMusic = await dbContext.BiblePublications
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.Language)
                    .AnyAsync(x => x.Category.CategoryName == MusicCategoryName 
                        && x.LanguageId != null 
                        && x.Language!.LanguageCode == lang.LanguageCode, cancellationToken);
                
                if (hasVocalMusic)
                {
                    vocalLanguages.Add(lang);
                }
            }

            logger.Information("VocalMusicService.GetDistinctLanguagesAsync: Found {PublicationLanguageCount} PublicationLanguage entries across {LanguageCount} distinct vocal music languages: {LanguageCodes}",
                publicationLanguagesCount, vocalLanguages.Count, string.Join(", ", vocalLanguages.Select(l => $"{l.LanguageCode}:{l.Name}")));

            return vocalLanguages.ToDictionary(x => x.LanguageCode, x => x);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting distinct Languages from PublicationLanguages for Music category");
            throw;
        }
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Language)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId != null 
                    && x.Language!.LanguageCode == languageCode 
                    && x.PublicationCode == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication == null)
            {
                return new SortedDictionary<int, MusicTrack>();
            }

            var tracks = publication.Tracks.Where(t => t.BiblePublicationSectionId == null).OrderBy(t => t.Number).ToList();
            // Map BiblePublicationTrack to MusicTrack
            var musicTracks = tracks.Select(t => new MusicTrack
            {
                Number = t.Number,
                Title = t.Title,
                Url = string.Empty, // URLs are computed on-demand
                LookUpPath = string.Empty,
                DownloadCode = null,
                OriginalTrackCode = null
            }).ToDictionary(x => x.Number, x => x);
            
            return new SortedDictionary<int, MusicTrack>(musicTracks);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting VocalMusic tracks. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default)
    {
        // URLs are now computed on-demand, no need to store them
        // This method is kept for backward compatibility but does nothing
        await Task.CompletedTask;
    }


    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
    }
}

