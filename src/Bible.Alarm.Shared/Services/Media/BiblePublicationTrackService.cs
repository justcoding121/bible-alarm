#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BiblePublicationTrack database operations.
/// </summary>
public sealed class BiblePublicationTrackService(IServiceScopeFactory scopeFactory, ILogger logger) : IBiblePublicationTrackService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    public async Task<SortedDictionary<int, BiblePublicationTrack>> GetTracksBySectionAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var tracks = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);

            return new SortedDictionary<int, BiblePublicationTrack>(tracks.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationTracks by section. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
                languageCode, publicationCode, sectionNumber);
            throw;
        }
    }

    public async Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, int sectionNumber, int trackNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationTrack. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}, TrackNumber={TrackNumber}",
                languageCode, publicationCode, sectionNumber, trackNumber);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string languageCode, string publicationCode, int sectionNumber, int trackNumber, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var track = await dbContext.BiblePublications
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
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
            logger.Error(ex, "Error updating BiblePublicationTrack URL. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}, TrackNumber={TrackNumber}",
                languageCode, publicationCode, sectionNumber, trackNumber);
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

