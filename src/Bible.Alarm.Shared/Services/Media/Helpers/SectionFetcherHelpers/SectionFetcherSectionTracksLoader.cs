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
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;

internal sealed class SectionFetcherSectionTracksLoader
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;

    public SectionFetcherSectionTracksLoader(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<bool> FetchSectionTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedSectionCode,
        string normalizedLanguageCode,
        string publicationCodeForDb,
        BiblePublication publication,
        BiblePublicationSection section,
        CancellationToken cancellationToken)
    {
        var entry = db.Entry(section);
        if (entry.State == EntityState.Detached)
        {
            logger.Debug("Section entity is detached, attaching to DbContext: sectionCode={SectionCode}", normalizedSectionCode);
            db.BiblePublicationSections.Attach(section);
        }

        await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
        if (section.Tracks != null && section.Tracks.Count > 0)
        {
            logger.Debug("Tracks already exist for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}, skipping fetch",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return true;
        }

        var categoryCode = publication.PrimaryCategory?.CategoryCode ?? "";
        var isBible = categoryCode.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        var isVideoDrama = !isBible && publication.IsVideo;
        var dramaFileFormat = isVideoDrama ? "MP4" : "MP3";

        // Section-level fetch only (no track=). Response contains all tracks for this section; we parse and create BiblePublicationTrack per file.
        var queryString = isBible
            ? $"?output=json&pub={normalizedPublicationCode}&booknum={normalizedSectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
            : $"?output=json&pub={normalizedSectionCode}&fileformat={dramaFileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
        if (jsonString == null)
        {
            logger.Warning("Failed to fetch tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            logger.Warning("Invalid response format for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        string? updatedSectionName = null;
        if (root.TryGetProperty("pubName", out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            logger.Debug("Found pubName in API response for section {SectionCode}: rawName={RawName}", normalizedSectionCode, rawName);
            var sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            if (!string.IsNullOrEmpty(sectionName))
            {
                var oldName = section.Name;
                updatedSectionName = sectionName;
                section.Name = sectionName;
                db.Entry(section).Property(s => s.Name).IsModified = true;
                logger.Information("Updated section name from API: {OldName} -> {NewName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. IsModified={IsModified}",
                    oldName, sectionName, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, db.Entry(section).Property(s => s.Name).IsModified);
            }
            else
                logger.Warning("pubName found in API response but section name is empty after processing. rawName={RawName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    rawName, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
        }
        else
            logger.Warning("pubName not found in API response for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Available properties: {Properties}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, string.Join(", ", root.EnumerateObject().Select(p => p.Name)));

        var formatKey = isVideoDrama ? "MP4" : "MP3";
        if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) || !languageFiles.TryGetProperty(formatKey, out var formatFiles))
        {
            logger.Warning("No {Format} files found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                formatKey, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var tracks = new List<BiblePublicationTrack>();
        var trackCode = 1;

        foreach (var trackFile in formatFiles.EnumerateArray())
        {
            if (!trackFile.TryGetProperty("file", out var fileElement))
                continue;
            string? url = null;
            if (fileElement.ValueKind == JsonValueKind.String)
                url = fileElement.GetString();
            else if (fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty("url", out var urlElement))
                url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            string title = "Unknown";
            if (trackFile.TryGetProperty("title", out var titleElement))
            {
                if (titleElement.ValueKind == JsonValueKind.String)
                {
                    var rawTitle = titleElement.GetString();
                    title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                }
                else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty("text", out var titleTextElement))
                {
                    var rawTitle = titleTextElement.GetString();
                    title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                }
                if (isBible && !string.IsNullOrEmpty(title) && title != "Unknown")
                {
                    var separators = new[] { " - ", " – ", " — ", " -", "- " };
                    foreach (var separator in separators)
                    {
                        if (title.Contains(separator))
                        {
                            var parts = title.Split(new[] { separator }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
                                title = parts[parts.Length - 1].Trim();
                                break;
                            }
                        }
                    }
                }
            }

            var track = new BiblePublicationTrack
            {
                TrackCode = isBible ? trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : normalizedSectionCode,
                Title = title,
                Publication = publication,
                BiblePublicationId = publication.Id,
                Section = section,
                BiblePublicationSectionId = section.Id,
                TrackUrl = new TrackUrl { Url = url }
            };

            tracks.Add(track);
            trackCode++;
        }

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        if (section.Tracks == null)
            section.Tracks = new List<BiblePublicationTrack>();
        foreach (var track in tracks)
            section.Tracks.Add(track);

        if (!string.IsNullOrEmpty(updatedSectionName) && section.Name != updatedSectionName)
        {
            logger.Warning("Section name was lost during track addition, re-applying: sectionCode={SectionCode}, expectedName={ExpectedName}, currentName={CurrentName}",
                normalizedSectionCode, updatedSectionName, section.Name);
            section.Name = updatedSectionName;
        }
        if (!string.IsNullOrEmpty(updatedSectionName))
        {
            db.Entry(section).Property(s => s.Name).IsModified = true;
            logger.Debug("Marking section name as modified before save: sectionCode={SectionCode}, name={Name}, isModified={IsModified}",
                normalizedSectionCode, section.Name, db.Entry(section).Property(s => s.Name).IsModified);
        }

        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(section).ReloadAsync(cancellationToken);
        var persistedSectionName = section.Name;

        if (!string.IsNullOrEmpty(updatedSectionName) && persistedSectionName != updatedSectionName)
            logger.Error("Section name was not persisted correctly! Expected: {ExpectedName}, Actual: {ActualName} for section {SectionCode}",
                updatedSectionName, persistedSectionName, normalizedSectionCode);

        logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Section name: {SectionName}",
            tracks.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, persistedSectionName);

        return true;
    }
}
