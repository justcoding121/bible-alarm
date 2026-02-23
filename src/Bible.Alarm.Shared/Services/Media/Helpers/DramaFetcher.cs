#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for fetching drama publications.
/// </summary>
internal sealed class DramaFetcher
{
    private readonly ILogger logger;
    private readonly DramaMediatorApiClient mediatorApiClient;
    private readonly DramaPublicationBuilder publicationBuilder;

    public DramaFetcher(HttpClient httpClient, ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.mediatorApiClient = new DramaMediatorApiClient(httpClient ?? throw new ArgumentNullException(nameof(httpClient)), this.logger);
        this.publicationBuilder = new DramaPublicationBuilder(this.logger);
    }

    public async Task<bool> FetchDramaPublicationTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        BiblePublication englishPublication,
        CancellationToken cancellationToken)
    {
        var (localizedPubName, mediatorTracks) = await mediatorApiClient.FetchCategoryAndTracksAsync(
            normalizedPublicationCode, normalizedLanguageCode, cancellationToken);

        logger.Information("DramaFetcher: Found {ItemCount} tracks from mediator for publication {PublicationCode} in language {LanguageCode}",
            mediatorTracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        if (mediatorTracks.Count == 0)
        {
            logger.Warning("DramaFetcher: No tracks found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var tracks = mediatorTracks.Select(m => new BiblePublicationTrack
        {
            TrackCode = m.TrackCode,
            Title = m.Title,
            TrackUrl = new TrackUrl { Url = m.Url }
        }).ToList();

        logger.Information("DramaFetcher: Fetched {TrackCount} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        if (tracks.Count == 0)
        {
            return false;
        }

        // Get language and category
        var language = await db.Languages
            .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
        
        if (language == null)
        {
            logger.Warning("Language {LanguageCode} not found", normalizedLanguageCode);
            return false;
        }

        var category = englishPublication.PrimaryCategory;
        if (category == null)
        {
            logger.Warning("Category not found for English publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        // Build and save publication
        var publicationCodeForDb = publicationBuilder.GetPublicationCodeForDb(normalizedPublicationCode);
        return await publicationBuilder.BuildAndSavePublicationAsync(
            db, publicationCodeForDb, localizedPubName, language, category, tracks, cancellationToken);
    }

    public async Task<bool> FetchEnglishDramaPublicationAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language language,
        Category category,
        CancellationToken cancellationToken)
    {
        var (localizedPubName, mediatorTracks) = await mediatorApiClient.FetchCategoryAndTracksAsync(
            normalizedPublicationCode, normalizedLanguageCode, cancellationToken);

        logger.Information("DramaFetcher: Found {ItemCount} tracks from mediator for publication {PublicationCode} in language {LanguageCode}",
            mediatorTracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        if (mediatorTracks.Count == 0)
        {
            logger.Warning("DramaFetcher: No tracks found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var tracks = mediatorTracks.Select(m => new BiblePublicationTrack
        {
            TrackCode = m.TrackCode,
            Title = m.Title,
            TrackUrl = new TrackUrl { Url = m.Url }
        }).ToList();

        logger.Information("DramaFetcher: Fetched {TrackCount} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        // Build and save publication
        var publicationCodeForDb = publicationBuilder.GetPublicationCodeForDb(normalizedPublicationCode);
        return await publicationBuilder.BuildAndSavePublicationAsync(
            db, publicationCodeForDb, localizedPubName, language, category, tracks, cancellationToken);
    }
}
