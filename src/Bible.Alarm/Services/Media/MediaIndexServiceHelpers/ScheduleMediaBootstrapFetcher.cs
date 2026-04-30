#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Fetches missing non-EnglishSpanish publication/section data into the new media index during
/// bootstrap. Only fetches pub/section that are valid (present in discovery tables; discovery has
/// all languages including E/S). Invalid pub/section are skipped; salvage cleanup handles deletion.
/// </summary>
internal sealed class ScheduleMediaBootstrapFetcher(
    ILogger logger,
    ILanguageContentService languageContentService,
    IServiceScopeFactory scopeFactory)
{
    private const string SpanishLanguageCode = "S";
    private const string SqlParamPubCode = "@pubCode";
    private const string SqlParamLangCode = "@langCode";

    internal record ScheduleMediaReference(
        string PublicationCode,
        string LanguageCode,
        string? SectionCode,
        string? TrackCode);

    /// <summary>
    /// Fetches only for refs whose pub (and section when present) are in discovery tables.
    /// When mediaIndexDbPath is provided, invalid pub/section are skipped; salvage does cleanup.
    /// </summary>
    public async Task FetchMissingAsync(string scheduleDbPath, string? mediaIndexDbPath = null)
    {
        if (!File.Exists(scheduleDbPath))
        {
            logger.Debug("Schedule DB not found at {Path}, skipping schedule media bootstrap fetch", scheduleDbPath);
            return;
        }

        var references = await ReadNonEnglishSpanishReferencesAsync(scheduleDbPath);
        if (references.Count == 0)
        {
            logger.Information("No non-EnglishSpanish schedule references found; skipping schedule media bootstrap fetch");
            return;
        }

        if (!string.IsNullOrEmpty(mediaIndexDbPath) && File.Exists(mediaIndexDbPath))
        {
            references = await FilterToValidPubAndSectionInDiscoveryAsync(mediaIndexDbPath, references);
            if (references.Count == 0)
            {
                logger.Information("No non-EnglishSpanish references with valid (discovery) pub/section; skipping fetch");
                return;
            }
        }

        logger.Information(
            "Fetching missing non-EnglishSpanish media for {Count} valid schedule reference(s) into new media index",
            references.Count);

        var publicationGroups = references
            .GroupBy(r => (r.PublicationCode, r.LanguageCode), PublicationLookupKeyComparers.PublicationLanguage.Instance)
            .ToList();

        foreach (var group in publicationGroups)
        {
            var pubCode = group.Key.PublicationCode;
            var langCode = group.Key.LanguageCode;
            var sectionCodes = group
                .Where(r => r.SectionCode != null)
                .Select(r => r.SectionCode!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            try
            {
                var pubOk = await languageContentService.EnsurePublicationExistsAsync(
                    pubCode,
                    langCode,
                    progress: null,
                    cancellationToken: CancellationToken.None);

                if (!pubOk)
                {
                    logger.Warning(
                        "Failed to ensure publication {PubCode}/{LangCode} exists in new media index",
                        pubCode, langCode);
                    continue;
                }

                foreach (var sectionCode in sectionCodes)
                {
                    try
                    {
                        await EnsureSectionEntityExistsAsync(pubCode, sectionCode, langCode);

                        var sectionOk = await languageContentService.FetchSectionTracksAsync(
                            pubCode,
                            sectionCode,
                            langCode,
                            cancellationToken: CancellationToken.None);

                        if (!sectionOk)
                        {
                            logger.Warning(
                                "Failed to fetch section {SectionCode} for {PubCode}/{LangCode}",
                                sectionCode, pubCode, langCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex,
                            "Error fetching section {SectionCode} for {PubCode}/{LangCode}",
                            sectionCode, pubCode, langCode);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex,
                    "Error ensuring publication {PubCode}/{LangCode} during bootstrap fetch",
                    pubCode, langCode);
            }
        }

        logger.Information("Schedule media bootstrap fetch completed");
    }

    internal static async Task<List<ScheduleMediaReference>> ReadNonEnglishSpanishReferencesAsync(
        string scheduleDbPath)
    {
        var references = new List<ScheduleMediaReference>();
        using var connection = new SqliteConnection($"Data Source={scheduleDbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT PublicationCode, LanguageCode, SectionCode, TrackCode
            FROM BiblePublicationSchedules
            WHERE LanguageCode IS NOT NULL AND LanguageCode NOT IN (@e, @s)
            UNION
            SELECT DISTINCT PublicationCode, LanguageCode, SectionCode, TrackCode
            FROM AlarmMusic
            WHERE LanguageCode IS NOT NULL AND LanguageCode NOT IN (@e, @s)
            """;
        cmd.Parameters.AddWithValue("@e", AppConstants.Media.DefaultLanguageCode);
        cmd.Parameters.AddWithValue("@s", SpanishLanguageCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            references.Add(new ScheduleMediaReference(
                reader.GetString(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2) ? null : reader.GetString(2),
                await reader.IsDBNullAsync(3) ? null : reader.GetString(3)));
        }

        return references;
    }

    /// <summary>
    /// Keeps only refs whose pub is in PublicationLanguages and (when section present) section is in SectionLanguages.
    /// Discovery tables include all languages (E/S and others).
    /// </summary>
    internal static async Task<List<ScheduleMediaReference>> FilterToValidPubAndSectionInDiscoveryAsync(
        string mediaIndexDbPath,
        List<ScheduleMediaReference> references)
    {
        var filtered = new List<ScheduleMediaReference>();
        using var connection = new SqliteConnection($"Data Source={mediaIndexDbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        foreach (var r in references)
        {
            if (!await PublicationExistsInDiscoveryAsync(connection, r.PublicationCode, r.LanguageCode)
                || (!string.IsNullOrEmpty(r.SectionCode)
                    && !await SectionExistsInDiscoveryAsync(connection, r.PublicationCode, r.SectionCode, r.LanguageCode)))
            {
                continue;
            }

            filtered.Add(r);
        }

        return filtered;
    }

    internal static async Task<bool> PublicationExistsInDiscoveryAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(1) FROM PublicationLanguages pl
            JOIN Languages l ON pl.LanguageId = l.Id
            WHERE pl.PublicationCode = {SqlParamPubCode} AND l.LanguageCode = {SqlParamLangCode}
            """;
        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    internal static async Task<bool> SectionExistsInDiscoveryAsync(
        SqliteConnection connection,
        string pubCode,
        string sectionCode,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(1) FROM SectionLanguages sl
            JOIN Languages l ON sl.LanguageId = l.Id
            WHERE sl.PublicationCode = {SqlParamPubCode} AND sl.SectionCode = @sectionCode AND l.LanguageCode = {SqlParamLangCode}
            """;
        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue("@sectionCode", sectionCode);
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    /// <summary>
    /// For sectioned publications, EnsurePublicationExistsAsync only creates the first section.
    /// This creates the section entity (without tracks) for other referenced sections so that
    /// FetchSectionTracksAsync can find it and fetch its tracks.
    /// </summary>
    private async Task EnsureSectionEntityExistsAsync(string pubCode, string sectionCode, string langCode)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var normalizedPubCode = pubCode.ToLowerInvariant();
        var normalizedSectionCode = sectionCode.ToLowerInvariant();
        var normalizedLangCode = langCode.ToUpperInvariant();
        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPubCode) ?? pubCode;

        var publication = await db.BiblePublications
            .Include(bp => bp.Sections)
            .Include(bp => bp.Language)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == publicationCodeForDb &&
                      bp.Language != null &&
                      bp.Language.LanguageCode == normalizedLangCode);

        if (publication == null)
        {
            return;
        }

        var sectionExists = publication.Sections.Any(s =>
            s.SectionCode.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase));

        if (sectionExists)
        {
            return;
        }

        var section = new BiblePublicationSection
        {
            Name = sectionCode,
            SectionCode = normalizedSectionCode,
            BiblePublication = publication,
            BiblePublicationId = publication.Id,
            Tracks = new List<BiblePublicationTrack>()
        };
        publication.Sections.Add(section);
        await db.SaveChangesAsync();

        logger.Debug(
            "Created missing section entity {SectionCode} for {PubCode}/{LangCode} during bootstrap",
            sectionCode, pubCode, langCode);
    }
}
