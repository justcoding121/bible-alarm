#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing MelodyMusic database operations.
/// </summary>
public sealed class MelodyMusicService(IServiceScopeFactory scopeFactory, ILogger logger) : IMelodyMusicService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    public async Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.MelodyMusic
                .AsNoTracking()
                .Include(x => x.Tracks)
                .Where(x => x.Code == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);
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

            var melodyList = await dbContext.MelodyMusic
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Handle potential duplicates gracefully - use first occurrence
            var result = new Dictionary<string, MelodyMusic>();
            foreach (var melody in melodyList)
            {
                if (!result.ContainsKey(melody.Code))
                {
                    result[melody.Code] = melody;
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

            var tracks = await dbContext.MelodyMusic
                .AsNoTracking()
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);

            return new SortedDictionary<int, MusicTrack>(tracks.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting MelodyMusic tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string publicationCode, int trackNumber, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var track = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .ThenInclude(s => s!.BaseUrlEntity)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (track?.Source != null)
            {
                // Extract path from the new URL and update UrlPath
                var (baseUrl, urlPath) = ExtractBaseUrlAndPath(url);
                track.Source.UrlPath = urlPath;

                // Update BaseUrl if it changed (rare, but handle it)
                if (track.Source.BaseUrlEntity.BaseUrl != baseUrl)
                {
                    var existingBaseUrl = await dbContext.SourceBaseUrls
                        .FirstOrDefaultAsync(x => x.BaseUrl == baseUrl, cancellationToken);
                    if (existingBaseUrl != null)
                    {
                        track.Source.BaseUrlEntity = existingBaseUrl;
                    }
                    else
                    {
                        track.Source.BaseUrlEntity = new SourceBaseUrl { BaseUrl = baseUrl };
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MelodyMusic track URL. PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
                publicationCode, trackNumber);
            throw;
        }
    }

    private static (string BaseUrl, string UrlPath) ExtractBaseUrlAndPath(string fullUrl)
    {
        if (string.IsNullOrEmpty(fullUrl))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var uri = new Uri(fullUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var urlPath = uri.PathAndQuery;
            return (baseUrl, urlPath);
        }
        catch (UriFormatException)
        {
            return (string.Empty, fullUrl);
        }
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

