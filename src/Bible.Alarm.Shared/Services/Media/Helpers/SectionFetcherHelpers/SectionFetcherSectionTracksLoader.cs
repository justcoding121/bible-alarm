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
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
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

    public async Task<bool> FetchSectionTracksAsync(FetchSectionTracksRequest request)
    {
        var db = request.Db;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var normalizedSectionCode = request.NormalizedSectionCode;
        var normalizedLanguageCode = request.NormalizedLanguageCode;
        var publicationCodeForDb = request.PublicationCodeForDb;
        var publication = request.Publication;
        var section = request.Section;
        var cancellationToken = request.CancellationToken;
        var replaceExisting = request.ReplaceExisting;

        var entry = db.Entry(section);
        if (entry.State == EntityState.Detached)
        {
            logger.Debug("Section entity is detached, attaching to DbContext: sectionCode={SectionCode}", normalizedSectionCode);
            db.BiblePublicationSections.Attach(section);
        }

        await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
        if (!replaceExisting && section.Tracks != null && section.Tracks.Count > 0)
        {
            logger.Debug("Tracks already exist for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}, skipping fetch",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return true;
        }

        var existingTracksToReplace = new List<BiblePublicationTrack>();
        if (replaceExisting)
        {
            existingTracksToReplace = await db.BiblePublicationTracks
                .Include(t => t.TrackUrl)
                .Where(t => t.BiblePublicationSectionId == section.Id)
                .ToListAsync(cancellationToken);
        }

        var categoryCode = publication.PrimaryCategory?.CategoryCode ?? "";
        var isBible = categoryCode.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        var isIssueSectioned = publication.CatalogType == CatalogType.IssueSectioned ||
            MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode);
        var isVideoDrama = !isBible && !isIssueSectioned && publication.IsVideo;
        var dramaFileFormat = isVideoDrama ? "MP4" : "MP3";

        string queryString;
        if (isIssueSectioned)
        {
            var (apiPubCode, issueCode) = MagazineHelper.ParseSectionCode(normalizedSectionCode);
            queryString = $"?output=json&pub={apiPubCode}&issue={issueCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else if (isBible)
        {
            queryString = $"?output=json&pub={normalizedPublicationCode}&booknum={normalizedSectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else
        {
            queryString = $"?output=json&pub={normalizedSectionCode}&fileformat={dramaFileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }

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
        if (isIssueSectioned)
        {
            string? pubName = null;
            string? formattedDate = null;
            if (root.TryGetProperty("pubName", out var pnEl))
                pubName = pnEl.GetString();
            if (root.TryGetProperty("formattedDate", out var fdEl))
                formattedDate = fdEl.GetString();
            var sectionName = MagazineHelper.BuildSectionName(pubName, formattedDate);
            if (!string.IsNullOrEmpty(sectionName))
            {
                var oldName = section.Name;
                updatedSectionName = sectionName;
                section.Name = sectionName;
                db.Entry(section).Property(s => s.Name).IsModified = true;
                logger.Information("Updated magazine section name from API: {OldName} -> {NewName} for section {SectionCode}",
                    oldName, sectionName, normalizedSectionCode);
            }
        }
        else if (root.TryGetProperty("pubName", out var pubNameElement))
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

            string trackCodeStr;
            if (isBible)
            {
                trackCodeStr = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
                trackCode++;
            }
            else if (isIssueSectioned)
            {
                if (trackFile.TryGetProperty("track", out var issueTrackEl) &&
                    issueTrackEl.ValueKind == JsonValueKind.Number &&
                    issueTrackEl.TryGetInt32(out var issueTrackNum) &&
                    issueTrackNum > 0)
                {
                    trackCodeStr = issueTrackNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    continue;
                }
            }
            else if (publication.IsMusic && !publication.IsVideo)
            {
                if (trackFile.TryGetProperty("track", out var trackNumEl) &&
                    trackNumEl.ValueKind == JsonValueKind.Number &&
                    trackNumEl.TryGetInt32(out var apiTrackNum))
                {
                    trackCodeStr = apiTrackNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    trackCodeStr = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    trackCode++;
                }
            }
            else
            {
                trackCodeStr = normalizedSectionCode;
                trackCode++;
            }

            var track = new BiblePublicationTrack
            {
                TrackCode = trackCodeStr,
                Title = title,
                Publication = publication,
                BiblePublicationId = publication.Id,
                Section = section,
                BiblePublicationSectionId = section.Id,
                TrackUrl = new TrackUrl { Url = url }
            };

            tracks.Add(track);
        }

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        if (existingTracksToReplace.Count > 0)
        {
            logger.Information(
                "Replacing {Count} existing tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode} (API refresh)",
                existingTracksToReplace.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            foreach (var oldTrack in existingTracksToReplace)
            {
                if (oldTrack.TrackUrl is not null)
                {
                    db.TrackUrls.Remove(oldTrack.TrackUrl);
                }
            }

            db.BiblePublicationTracks.RemoveRange(existingTracksToReplace);
            section.Tracks?.Clear();
            await db.SaveChangesAsync(cancellationToken);
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

        await SaveTracksWithRetryAsync(new SaveSectionTracksPersistenceRequest(
            db, section, normalizedSectionCode, normalizedPublicationCode,
            normalizedLanguageCode, cancellationToken));
        await db.Entry(section).ReloadAsync(cancellationToken);
        var persistedSectionName = section.Name;

        if (!string.IsNullOrEmpty(updatedSectionName) && persistedSectionName != updatedSectionName)
            logger.Error("Section name was not persisted correctly! Expected: {ExpectedName}, Actual: {ActualName} for section {SectionCode}",
                updatedSectionName, persistedSectionName, normalizedSectionCode);

        logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Section name: {SectionName}",
            tracks.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, persistedSectionName);

        return true;
    }

    private async Task SaveTracksWithRetryAsync(SaveSectionTracksPersistenceRequest request)
    {
        var db = request.Db;
        var section = request.Section;
        var sectionCode = request.SectionCode;
        var publicationCode = request.PublicationCode;
        var languageCode = request.LanguageCode;
        var cancellationToken = request.CancellationToken;

        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsSqliteBusyOrLocked(ex))
            {
                logger.Debug("SaveChanges locked (attempt {Attempt}/{Max}) for section {SectionCode}, retrying",
                    attempt, maxAttempts, sectionCode);
                await Task.Delay(100 * attempt, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                logger.Warning("Unique constraint on tracks for section {SectionCode} in {PublicationCode}/{LanguageCode} " +
                    "- another operation likely inserted them concurrently",
                    sectionCode, publicationCode, languageCode);
                await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
                if (section.Tracks != null && section.Tracks.Count > 0)
                {
                    return;
                }
                throw;
            }
        }
    }

    private static bool IsSqliteBusyOrLocked(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException sqliteEx)
            {
                var code = (int)sqliteEx.SqliteErrorCode;
                if (code is 5 or 6)
                    return true;
            }
        }
        return false;
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException sqliteEx && (int)sqliteEx.SqliteErrorCode == 19)
                return true;
        }
        return false;
    }
}
