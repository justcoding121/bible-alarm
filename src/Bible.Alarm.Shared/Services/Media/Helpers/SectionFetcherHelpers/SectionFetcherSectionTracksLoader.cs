#nullable enable

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

        var categoryName = publication.Category?.CategoryName ?? "";
        var isBible = categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);

        var harvestLink = isBible
            ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={normalizedSectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
            : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedSectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

        var response = await httpClient.GetAsync(harvestLink, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.Warning("Failed to fetch tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
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

        if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) || !languageFiles.TryGetProperty("MP3", out var mp3Files))
        {
            logger.Warning("No MP3 files found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var baseUrl = await db.BaseUrls.Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS").FirstOrDefaultAsync(cancellationToken);
        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return false;
        }

        var tracks = new List<BiblePublicationTrack>();
        var trackCode = 1;

        foreach (var trackFile in mp3Files.EnumerateArray())
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
                TrackCode = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Title = title,
                Publication = publication,
                BiblePublicationId = publication.Id,
                Section = section,
                BiblePublicationSectionId = section.Id,
                UrlParams = new List<UrlParam>()
            };

            if (isBible)
            {
                track.UrlParams.Add(new UrlParam { Key = "pub", Value = normalizedPublicationCode, IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });
                track.UrlParams.Add(new UrlParam { Key = "booknum", Value = normalizedSectionCode, IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });
            }
            else
            {
                track.UrlParams.Add(new UrlParam { Key = "pub", Value = normalizedSectionCode, IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });
                track.TrackCode = normalizedSectionCode;
            }
            track.UrlParams.Add(new UrlParam { Key = "track", Value = trackCode.ToString(), IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });
            track.UrlParams.Add(new UrlParam { Key = "fileformat", Value = "mp3", IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });
            track.UrlParams.Add(new UrlParam { Key = "alllangs", Value = "0", IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });
            track.UrlParams.Add(new UrlParam { Key = "langwritten", Value = normalizedLanguageCode, IsQueryParam = true, BaseUrl = baseUrl, BaseUrlId = baseUrl.Id });

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
