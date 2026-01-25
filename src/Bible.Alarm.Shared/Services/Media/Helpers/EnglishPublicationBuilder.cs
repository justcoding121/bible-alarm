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
/// Helper class for building and saving English publications.
/// </summary>
internal sealed class EnglishPublicationBuilder
{
    private readonly ILogger logger;

    public EnglishPublicationBuilder(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> BuildAndSavePublicationAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string? publicationName,
        Language? language,
        Category category,
        bool isVideo,
        bool isBible,
        bool publicationWithoutLanguage,
        List<BiblePublicationSection> sections,
        CancellationToken cancellationToken)
    {
        if (sections.Count == 0)
        {
            logger.Warning("No sections found for publication {PublicationCode} in English", normalizedPublicationCode);
            return false;
        }

        var finalPublicationName = publicationName ?? normalizedPublicationCode;
        
        // For Bible publications, temporarily remove tracks from sections before saving
        // We'll add them back after sections have IDs to avoid foreign key constraint errors
        // Publications without language (like instrumental music) also have tracks in sections already
        var tracksBySection = new Dictionary<BiblePublicationSection, List<BiblePublicationTrack>>();
        if (isBible && !publicationWithoutLanguage)
        {
            foreach (var section in sections)
            {
                if (section.Tracks.Count > 0)
                {
                    tracksBySection[section] = section.Tracks.ToList();
                    section.Tracks.Clear();
                }
            }
        }
        
        var publication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
            Name = finalPublicationName,
            Language = language, // null for publications without language
            Category = category,
            CategoryId = category.Id,
            LanguageId = language?.Id, // null for publications without language
            IsVideo = isVideo,
            Tracks = new List<BiblePublicationTrack>(),
            Sections = sections
        };

        // Set publication reference on sections and tracks
        // For publications without language and non-Bible publications, tracks are already in sections
        foreach (var section in sections)
        {
            section.BiblePublication = publication;
            
            // For publications without language and other non-Bible publications, set references on tracks now
            // (Bible publications will set references after sections have IDs)
            if (!isBible || publicationWithoutLanguage)
            {
                foreach (var track in section.Tracks)
                {
                    track.Publication = publication;
                    track.Section = section;
                }
            }
        }

        // Save publication and sections first so they get IDs
        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);
        
        // For Bible publications, now add tracks back with proper foreign key IDs
        if (isBible && !publicationWithoutLanguage && tracksBySection.Count > 0)
        {
            foreach (var kvp in tracksBySection)
            {
                var section = kvp.Key;
                var tracks = kvp.Value;
                
                foreach (var track in tracks)
                {
                    track.Publication = publication;
                    track.Section = section;
                    track.BiblePublicationId = publication.Id;
                    track.BiblePublicationSectionId = section.Id;
                }
                
                // Add tracks to section and to DbContext
                foreach (var track in tracks)
                {
                    section.Tracks.Add(track);
                }
                db.BiblePublicationTracks.AddRange(tracks);
            }
            
            // Save tracks
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Information("Successfully seeded {Count} sections for English publication {PublicationCode}",
            sections.Count, normalizedPublicationCode);

        return true;
    }
}
