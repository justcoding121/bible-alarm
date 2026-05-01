#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        var isBible = categoryCode.Equals(AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase);
        var isIssueSectioned = publication.CatalogType == CatalogType.IssueSectioned ||
            MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode);
        var isVideoDrama = !isBible && !isIssueSectioned && publication.IsVideo;
        var dramaFileFormat = isVideoDrama ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;

        var queryString = BuildSectionTracksPubQuery(
            isIssueSectioned,
            isBible,
            normalizedPublicationCode,
            normalizedSectionCode,
            normalizedLanguageCode,
            dramaFileFormat);

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

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
        {
            logger.Warning("Invalid response format for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        string? updatedSectionName = null;
        ApplySectionNameFromPubMediaRoot(
            root,
            isIssueSectioned,
            section,
            db,
            normalizedSectionCode,
            normalizedPublicationCode,
            normalizedLanguageCode,
            ref updatedSectionName);

        var formatKey = isVideoDrama ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;
        if (!TryGetLanguageFormatFilesElement(
                filesElement,
                normalizedLanguageCode,
                formatKey,
                normalizedSectionCode,
                normalizedPublicationCode,
                out var formatFiles))
        {
            return false;
        }

        var tracks = BuildSectionTracksFromFormatFiles(
            formatFiles,
            isBible,
            isIssueSectioned,
            publication,
            section,
            normalizedSectionCode);

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
            foreach (var oldTrack in existingTracksToReplace.Where(t => t.TrackUrl is not null))
            {
                db.TrackUrls.Remove(oldTrack.TrackUrl!);
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

    private void ApplySectionNameFromPubMediaRoot(
        JsonElement root,
        bool isIssueSectioned,
        BiblePublicationSection section,
        MediaDbContext db,
        string normalizedSectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        ref string? updatedSectionName)
    {
        if (isIssueSectioned)
        {
            string? pubName = null;
            string? formattedDate = null;
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pnEl))
            {
                pubName = pnEl.GetString();
            }

            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.FormattedDate, out var fdEl))
            {
                formattedDate = fdEl.GetString();
            }

            var sectionName = MagazineHelper.BuildSectionName(pubName, formattedDate);
            if (string.IsNullOrEmpty(sectionName))
            {
                return;
            }

            var oldName = section.Name;
            updatedSectionName = sectionName;
            section.Name = sectionName;
            db.Entry(section).Property(s => s.Name).IsModified = true;
            logger.Information("Updated magazine section name from API: {OldName} -> {NewName} for section {SectionCode}",
                oldName, sectionName, normalizedSectionCode);
            return;
        }

        if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            logger.Warning(
                "pubName not found in API response for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Available properties: {Properties}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode,
                string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
            return;
        }

        var rawName = pubNameElement.GetString();
        logger.Debug("Found pubName in API response for section {SectionCode}: rawName={RawName}", normalizedSectionCode, rawName);
        var sectionNameFromApi = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
        if (string.IsNullOrEmpty(sectionNameFromApi))
        {
            logger.Warning(
                "pubName found in API response but section name is empty after processing. rawName={RawName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                rawName, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return;
        }

        var previousName = section.Name;
        updatedSectionName = sectionNameFromApi;
        section.Name = sectionNameFromApi;
        db.Entry(section).Property(s => s.Name).IsModified = true;
        logger.Information(
            "Updated section name from API: {OldName} -> {NewName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. IsModified={IsModified}",
            previousName, sectionNameFromApi, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode,
            db.Entry(section).Property(s => s.Name).IsModified);
    }

    private bool TryGetLanguageFormatFilesElement(
        JsonElement filesElement,
        string normalizedLanguageCode,
        string formatKey,
        string normalizedSectionCode,
        string normalizedPublicationCode,
        out JsonElement formatFiles)
    {
        formatFiles = default;
        if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
            !languageFiles.TryGetProperty(formatKey, out formatFiles))
        {
            logger.Warning(
                "No {Format} files found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                formatKey, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        return true;
    }

    private List<BiblePublicationTrack> BuildSectionTracksFromFormatFiles(
        JsonElement formatFiles,
        bool isBible,
        bool isIssueSectioned,
        BiblePublication publication,
        BiblePublicationSection section,
        string normalizedSectionCode)
    {
        var tracks = new List<BiblePublicationTrack>();
        var trackCode = 1;

        foreach (var trackFile in formatFiles.EnumerateArray())
        {
            var built = TryBuildSingleSectionTrackFromFile(
                trackFile,
                isBible,
                isIssueSectioned,
                publication,
                section,
                normalizedSectionCode,
                ref trackCode);
            if (built != null)
            {
                tracks.Add(built);
            }
        }

        return tracks;
    }

    private BiblePublicationTrack? TryBuildSingleSectionTrackFromFile(
        JsonElement trackFile,
        bool isBible,
        bool isIssueSectioned,
        BiblePublication publication,
        BiblePublicationSection section,
        string normalizedSectionCode,
        ref int trackCode)
    {
        if (!trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileElement))
        {
            return null;
        }

        string? url = null;
        if (fileElement.ValueKind == JsonValueKind.String)
        {
            url = fileElement.GetString();
        }
        else if (fileElement.ValueKind == JsonValueKind.Object &&
                 fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Url, out var urlElement))
        {
            url = urlElement.GetString();
        }

        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var title = ResolveDecodedTrackTitleFromFileElement(trackFile, isBible);

        if (!TryResolveSectionTrackCodeString(
                trackFile,
                isBible,
                isIssueSectioned,
                publication,
                normalizedSectionCode,
                ref trackCode,
                out var trackCodeStr))
        {
            return null;
        }

        return new BiblePublicationTrack
        {
            TrackCode = trackCodeStr,
            Title = title,
            Publication = publication,
            BiblePublicationId = publication.Id,
            Section = section,
            BiblePublicationSectionId = section.Id,
            TrackUrl = new TrackUrl { Url = url }
        };
    }

    private static string ResolveDecodedTrackTitleFromFileElement(JsonElement trackFile, bool isBible)
    {
        var title = MediaTrackTitleHelper.UnknownTitle;
        if (!trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
        {
            return title;
        }

        if (titleElement.ValueKind == JsonValueKind.String)
        {
            title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
        }
        else if (titleElement.ValueKind == JsonValueKind.Object &&
                 titleElement.TryGetProperty(AppConstants.Media.PubMediaJson.Text, out var titleTextElement))
        {
            title = MediaTrackTitleHelper.DecodeHtmlTitle(titleTextElement.GetString());
        }

        return ApplyBibleTrackTitleChapterSplit(title, isBible);
    }

    private static string ApplyBibleTrackTitleChapterSplit(string title, bool isBible)
    {
        if (!isBible || string.IsNullOrEmpty(title) || title == MediaTrackTitleHelper.UnknownTitle)
        {
            return title;
        }

        var separators = new[] { " - ", " – ", " — ", " -", "- " };
        foreach (var separator in separators.Where(sep => title.Contains(sep, StringComparison.Ordinal)))
        {
            var parts = title.Split(new[] { separator }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                return parts[parts.Length - 1].Trim();
            }
        }

        return title;
    }

    private bool TryResolveSectionTrackCodeString(
        JsonElement trackFile,
        bool isBible,
        bool isIssueSectioned,
        BiblePublication publication,
        string normalizedSectionCode,
        ref int trackCode,
        out string trackCodeStr)
    {
        if (isBible)
        {
            trackCodeStr = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            trackCode++;
            return true;
        }

        if (isIssueSectioned)
        {
            if (trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var issueTrackEl) &&
                issueTrackEl.ValueKind == JsonValueKind.Number &&
                issueTrackEl.TryGetInt32(out var issueTrackNum) &&
                issueTrackNum > 0)
            {
                trackCodeStr = issueTrackNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            trackCodeStr = null!;
            return false;
        }

        if (publication.IsMusic && !publication.IsVideo)
        {
            if (trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var trackNumEl) &&
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

            return true;
        }

        trackCodeStr = normalizedSectionCode;
        trackCode++;
        return true;
    }

    private static string BuildSectionTracksPubQuery(
        bool isIssueSectioned,
        bool isBible,
        string normalizedPublicationCode,
        string normalizedSectionCode,
        string normalizedLanguageCode,
        string dramaFileFormat)
    {
        if (isIssueSectioned)
        {
            var (apiPubCode, issueCode) = MagazineHelper.ParseSectionCode(normalizedSectionCode);
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={apiPubCode}&{AppConstants.Media.GetPubQueryParamName.Issue}={issueCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

        if (isBible)
        {
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={normalizedPublicationCode}&{AppConstants.Media.GetPubQueryParamName.BookNum}={normalizedSectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

        return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={normalizedSectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={dramaFileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
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
            catch (DbUpdateException ex) when (attempt < maxAttempts && SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(ex))
            {
                logger.Debug(ex, "SaveChanges locked (attempt {Attempt}/{Max}) for section {SectionCode}, retrying",
                    attempt, maxAttempts, sectionCode);
                await Task.Delay(100 * attempt, cancellationToken);
            }
            catch (DbUpdateException ex) when (SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(ex))
            {
                logger.Warning(ex, "Unique constraint on tracks for section {SectionCode} in {PublicationCode}/{LanguageCode} " +
                    "- another operation likely inserted them concurrently",
                    sectionCode, publicationCode, languageCode);
                await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
                if (section.Tracks != null && section.Tracks.Count > 0)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Unique constraint after concurrent insert but no tracks loaded for section {sectionCode} in publication {publicationCode} for language {languageCode}.",
                    ex);
            }
        }
    }
}

