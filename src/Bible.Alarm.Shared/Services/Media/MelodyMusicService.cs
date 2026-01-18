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
/// Service for accessing MelodyMusic database operations.
/// Uses BiblePublications table filtered by Music category without LanguageId (Kingdom Melodies).
/// </summary>
public sealed class MelodyMusicService(IServiceScopeFactory scopeFactory, ILogger logger) : IMelodyMusicService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;
    private const string MusicCategoryName = "Music";

    public async Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId == null 
                    && x.Code == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            // MelodyMusic is a subclass of BiblePublication, so we can return the publication directly
            // The publication is already filtered to be Music category without LanguageId
            return publication != null ? new MelodyMusic { Publication = publication } : null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting MelodyMusic with Tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var melodyList = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.Category.CategoryName == MusicCategoryName && x.LanguageId == null)
                .ToListAsync(cancellationToken);

            // Handle potential duplicates gracefully - use first occurrence
            var result = new Dictionary<string, MelodyMusic>();
            foreach (var melody in melodyList)
            {
                if (!result.ContainsKey(melody.Code))
                {
                    result[melody.Code] = new MelodyMusic { Publication = melody };
                }
                else
                {
                    logger.Warning("Duplicate MelodyMusic entry found. Code={Code}", melody.Code);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting all MelodyMusic");
            throw;
        }
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId == null 
                    && x.Code == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication == null)
            {
                return new SortedDictionary<int, MusicTrack>();
            }

            var tracks = publication.Tracks.OrderBy(t => t.Number).ToList();
            // Map BiblePublicationTrack to MusicTrack
            var musicTracks = tracks.Select(t => new MusicTrack
            {
                Number = t.Number,
                Title = t.Title,
                Url = string.Empty, // URLs are computed on-demand
                LookUpPath = string.Empty,
                DownloadCode = null,
                OriginalTrackNumber = null
            }).ToDictionary(x => x.Number, x => x);
            
            return new SortedDictionary<int, MusicTrack>(musicTracks);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting MelodyMusic tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string publicationCode, int trackNumber, string url, CancellationToken cancellationToken = default)
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

