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
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly DramaMediatorApiClient mediatorApiClient;
    private readonly DramaTrackFetcher trackFetcher;
    private readonly DramaPublicationBuilder publicationBuilder;

    public DramaFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var trackParser = new DramaTrackParser(logger);
        this.mediatorApiClient = new DramaMediatorApiClient(httpClient, logger);
        this.trackFetcher = new DramaTrackFetcher(httpClient, logger, trackParser);
        this.publicationBuilder = new DramaPublicationBuilder(logger);
    }

    public async Task<bool> FetchDramaPublicationTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        BiblePublication englishPublication,
        CancellationToken cancellationToken)
    {
        // Fetch category and section codes from Mediator API
        var (localizedPubName, sectionCodes) = await mediatorApiClient.FetchCategoryAndSectionsAsync(
            normalizedPublicationCode, normalizedLanguageCode, cancellationToken);

        logger.Information("DramaFetcher: Found {SectionCount} sections for publication {PublicationCode} in language {LanguageCode}",
            sectionCodes.Count, normalizedPublicationCode, normalizedLanguageCode);

        if (sectionCodes.Count == 0)
        {
            logger.Warning("DramaFetcher: No sections found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Fetch tracks for all sections
        var tracks = await trackFetcher.FetchTracksForSectionsAsync(
            db, sectionCodes, normalizedPublicationCode, normalizedLanguageCode, cancellationToken);
        
        logger.Information("DramaFetcher: Fetched {TrackCount} tracks total from {SectionCount} sections for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, sectionCodes.Count, normalizedPublicationCode, normalizedLanguageCode);

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
        // Fetch category and section codes from Mediator API
        var (localizedPubName, sectionCodes) = await mediatorApiClient.FetchCategoryAndSectionsAsync(
            normalizedPublicationCode, normalizedLanguageCode, cancellationToken);

        logger.Information("DramaFetcher: Found {SectionCount} sections for publication {PublicationCode} in language {LanguageCode}",
            sectionCodes.Count, normalizedPublicationCode, normalizedLanguageCode);

        if (sectionCodes.Count == 0)
        {
            logger.Warning("DramaFetcher: No sections found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Fetch tracks for all sections
        var tracks = await trackFetcher.FetchTracksForSectionsAsync(
            db, sectionCodes, normalizedPublicationCode, normalizedLanguageCode, cancellationToken);
        
        logger.Information("DramaFetcher: Fetched {TrackCount} tracks total from {SectionCount} sections for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, sectionCodes.Count, normalizedPublicationCode, normalizedLanguageCode);

        // Build and save publication
        var publicationCodeForDb = publicationBuilder.GetPublicationCodeForDb(normalizedPublicationCode);
        return await publicationBuilder.BuildAndSavePublicationAsync(
            db, publicationCodeForDb, localizedPubName, language, category, tracks, cancellationToken);
    }
}
