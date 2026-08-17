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
    private readonly record struct ApplySectionNameContext(
        MediaDbContext Db,
        string NormalizedSectionCode,
        string NormalizedPublicationCode,
        string NormalizedLanguageCode);

    private readonly record struct SectionTracksCatalogMode(
        bool IsBible,
        bool IsIssueSectioned,
        bool IsVideoDrama,
        string DramaFileFormat);

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

        AttachSectionIfDetached(db, section, normalizedSectionCode);

        await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
        if (!replaceExisting && section.Tracks != null && section.Tracks.Count > 0)
        {
            logger.Debug("Tracks already exist for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}, skipping fetch",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return true;
        }

        var existingTracksToReplace = await LoadExistingTracksForReplaceAsync(db, section, replaceExisting, cancellationToken);

        var mode = GetSectionTracksCatalogMode(publication, normalizedPublicationCode);
        var queryString = BuildSectionTracksPubQuery(
            mode.IsIssueSectioned,
            mode.IsBible,
            normalizedPublicationCode,
            normalizedSectionCode,
            normalizedLanguageCode,
            mode.DramaFileFormat);

        var jsonString = await FetchSectionTracksPubMediaJsonAsync(queryString, cancellationToken);
        if (jsonString == null)
        {
            logger.Warning("Failed to fetch tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }
        return await PersistFetchedSectionTracksAsync(request, existingTracksToReplace, jsonString);
    }

    private void AttachSectionIfDetached(MediaDbContext db, BiblePublicationSection section, string normalizedSectionCode)
    {
        var entry = db.Entry(section);
        if (entry.State == EntityState.Detached)
        {
            logger.Debug("Section entity is detached, attaching to DbContext: sectionCode={SectionCode}", normalizedSectionCode);
            db.BiblePublicationSections.Attach(section);
        }
    }

    private static async Task<List<BiblePublicationTrack>> LoadExistingTracksForReplaceAsync(
        MediaDbContext db,
        BiblePublicationSection section,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        if (!replaceExisting)
        {
            return new List<BiblePublicationTrack>();
        }

        return await db.BiblePublicationTracks
            .Include(t => t.TrackUrl)
            .Where(t => t.BiblePublicationSectionId == section.Id)
            .ToListAsync(cancellationToken);
    }

    private static SectionTracksCatalogMode GetSectionTracksCatalogMode(BiblePublication publication, string normalizedPublicationCode)
    {
        var categoryCode = publication.PrimaryCategory?.CategoryCode ?? "";
        var isBible = categoryCode.Equals(AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase);
        var isIssueSectioned = publication.CatalogType == CatalogType.IssueSectioned ||
            MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode);
        var isVideoDrama = !isBible && !isIssueSectioned && publication.IsVideo;
        var dramaFileFormat = isVideoDrama ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;
        return new SectionTracksCatalogMode(isBible, isIssueSectioned, isVideoDrama, dramaFileFormat);
    }

    private async Task<string?> FetchSectionTracksPubMediaJsonAsync(string queryString, CancellationToken cancellationToken)
    {
        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        return await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
    }

    private async Task<bool> PersistFetchedSectionTracksAsync(
        FetchSectionTracksRequest request,
        List<BiblePublicationTrack> existingTracksToReplace,
        string jsonString)
    {
        var db = request.Db;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var normalizedSectionCode = request.NormalizedSectionCode;
        var normalizedLanguageCode = request.NormalizedLanguageCode;
        var publication = request.Publication;
        var section = request.Section;
        var cancellationToken = request.CancellationToken;

        var mode = GetSectionTracksCatalogMode(publication, normalizedPublicationCode);
        var isBible = mode.IsBible;
        var isIssueSectioned = mode.IsIssueSectioned;
        var isVideoDrama = mode.IsVideoDrama;

        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
        {
            logger.Warning("Invalid response format for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var nameCtx = new ApplySectionNameContext(db, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
        var updatedSectionName = ApplySectionNameFromPubMediaRoot(
            root,
            isIssueSectioned,
            section,
            nameCtx);

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
        {
            section.Tracks = new List<BiblePublicationTrack>();
        }

        foreach (var track in tracks)
        {
            section.Tracks.Add(track);
        }

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
        {
            logger.Error("Section name was not persisted correctly! Expected: {ExpectedName}, Actual: {ActualName} for section {SectionCode}",
                updatedSectionName, persistedSectionName, normalizedSectionCode);
        }

        logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Section name: {SectionName}",
            tracks.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, persistedSectionName);

        return true;
    }

    private string? ApplySectionNameFromPubMediaRoot(
        JsonElement root,
        bool isIssueSectioned,
        BiblePublicationSection section,
        ApplySectionNameContext ctx)
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
                return null;
            }

            var oldName = section.Name;
            section.Name = sectionName;
            ctx.Db.Entry(section).Property(s => s.Name).IsModified = true;
            logger.Information("Updated magazine section name from API: {OldName} -> {NewName} for section {SectionCode}",
                oldName, sectionName, ctx.NormalizedSectionCode);
            return sectionName;
        }

        if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            logger.Warning(
                "pubName not found in API response for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Available properties: {Properties}",
                ctx.NormalizedSectionCode, ctx.NormalizedPublicationCode, ctx.NormalizedLanguageCode,
                string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
            return null;
        }

        var rawName = pubNameElement.GetString();
        logger.Debug("Found pubName in API response for section {SectionCode}: rawName={RawName}", ctx.NormalizedSectionCode, rawName);
        var sectionNameFromApi = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
        if (string.IsNullOrEmpty(sectionNameFromApi))
        {
            logger.Warning(
                "pubName found in API response but section name is empty after processing. rawName={RawName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                rawName, ctx.NormalizedSectionCode, ctx.NormalizedPublicationCode, ctx.NormalizedLanguageCode);
            return null;
        }

        var previousName = section.Name;
        section.Name = sectionNameFromApi;
        ctx.Db.Entry(section).Property(s => s.Name).IsModified = true;
        logger.Information(
            "Updated section name from API: {OldName} -> {NewName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. IsModified={IsModified}",
            previousName, sectionNameFromApi, ctx.NormalizedSectionCode, ctx.NormalizedPublicationCode, ctx.NormalizedLanguageCode,
            ctx.Db.Entry(section).Property(s => s.Name).IsModified);
        return sectionNameFromApi;
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

    private readonly record struct BuiltSectionTrackResult(BiblePublicationTrack? Track, int NextTrackCode);

    private static List<BiblePublicationTrack> BuildSectionTracksFromFormatFiles(
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
                trackCode);
            trackCode = built.NextTrackCode;
            if (built.Track != null)
            {
                tracks.Add(built.Track);
            }
        }

        return tracks;
    }

    private static BuiltSectionTrackResult TryBuildSingleSectionTrackFromFile(
        JsonElement trackFile,
        bool isBible,
        bool isIssueSectioned,
        BiblePublication publication,
        BiblePublicationSection section,
        string normalizedSectionCode,
        int trackCode)
    {
        if (!trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileElement))
        {
            return new BuiltSectionTrackResult(null, trackCode);
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
            return new BuiltSectionTrackResult(null, trackCode);
        }

        var title = ResolveDecodedTrackTitleFromFileElement(trackFile, isBible);

        var resolved = TryResolveSectionTrackCodeString(
            trackFile,
            isBible,
            isIssueSectioned,
            publication,
            normalizedSectionCode,
            trackCode);
        if (!resolved.Succeeded)
        {
            return new BuiltSectionTrackResult(null, resolved.NextTrackCode);
        }

        return new BuiltSectionTrackResult(
            new BiblePublicationTrack
            {
                TrackCode = resolved.TrackCodeStr!,
                Title = title,
                Publication = publication,
                BiblePublicationId = publication.Id,
                Section = section,
                BiblePublicationSectionId = section.Id,
                TrackUrl = new TrackUrl { Url = url }
            },
            resolved.NextTrackCode);
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

    private readonly record struct ResolvedSectionTrackCode(bool Succeeded, string? TrackCodeStr, int NextTrackCode);

    private static ResolvedSectionTrackCode TryResolveSectionTrackCodeString(
        JsonElement trackFile,
        bool isBible,
        bool isIssueSectioned,
        BiblePublication publication,
        string normalizedSectionCode,
        int trackCode)
    {
        if (isBible)
        {
            var trackCodeStr = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new ResolvedSectionTrackCode(true, trackCodeStr, trackCode + 1);
        }

        if (isIssueSectioned)
        {
            if (trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var issueTrackEl) &&
                issueTrackEl.ValueKind == JsonValueKind.Number &&
                issueTrackEl.TryGetInt32(out var issueTrackNum) &&
                issueTrackNum > 0)
            {
                var trackCodeStr = issueTrackNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return new ResolvedSectionTrackCode(true, trackCodeStr, trackCode);
            }

            return new ResolvedSectionTrackCode(false, null, trackCode);
        }

        if (publication.IsMusic && !publication.IsVideo)
        {
            if (trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var trackNumEl) &&
                trackNumEl.ValueKind == JsonValueKind.Number &&
                trackNumEl.TryGetInt32(out var apiTrackNum))
            {
                var trackCodeStr = apiTrackNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return new ResolvedSectionTrackCode(true, trackCodeStr, trackCode);
            }

            return new ResolvedSectionTrackCode(
                true,
                trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                trackCode + 1);
        }

        return new ResolvedSectionTrackCode(true, normalizedSectionCode, trackCode + 1);
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

