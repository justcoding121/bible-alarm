#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for building and saving drama publications.
/// </summary>
internal sealed class DramaPublicationBuilder
{
    private readonly ILogger logger;

    public DramaPublicationBuilder(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> BuildAndSavePublicationAsync(
        MediaDbContext db,
        string publicationCodeForDb,
        string? publicationName,
        Language language,
        Category category,
        List<BiblePublicationTrack> tracks,
        CancellationToken cancellationToken)
    {
        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for drama {PublicationCode}", publicationCodeForDb);
            return false;
        }

        var finalPublicationName = publicationName ?? publicationCodeForDb;
        var isVideo = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.IsVideo(publicationCodeForDb);
        var publication = new BiblePublication
        {
            PublicationCode = publicationCodeForDb,
            Name = finalPublicationName,
            Language = language,
            Category = category,
            CategoryId = category.Id,
            LanguageId = language.Id,
            IsVideo = isVideo,
            Tracks = tracks,
            Sections = new List<BiblePublicationSection>()
        };

        // Set publication reference on tracks
        foreach (var track in tracks)
        {
            track.Publication = publication;
            track.Section = null; // Dramas have flat structure, no sections
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for drama {PublicationCode}",
            tracks.Count, publicationCodeForDb);

        return true;
    }

    public string GetPublicationCodeForDb(string normalizedPublicationCode)
    {
        var canonical = Bible.Alarm.Shared.Helpers.JwSourceHelper.GetCanonicalDramaPublicationCode(normalizedPublicationCode);
        if (canonical != null)
        {
            return canonical;
        }

        logger.Warning("Unknown drama publication code: {PublicationCode}", normalizedPublicationCode);
        return normalizedPublicationCode;
    }
}
