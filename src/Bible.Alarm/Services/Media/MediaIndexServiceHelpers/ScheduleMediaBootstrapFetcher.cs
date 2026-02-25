#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Fetches missing non-English publication/section data into the new media index during bootstrap
/// after a version update. Runs after OrphanedScheduleCleanup (schedules with pub codes not in the
/// new index are already deleted). Reads schedule references (PublicationCode, LanguageCode,
/// SectionCode only—CategoryCode on AlarmSchedule is not used) and ensures each (PublicationCode,
/// LanguageCode) and referenced section is harvested. No retries—failed fetches are logged and skipped;
/// if all schedules were removed by cleanup, ScheduleBootstrapService.SeedAndMigrateAsync seeds a
/// default schedule on home load.
/// </summary>
internal sealed class ScheduleMediaBootstrapFetcher(ILogger logger, ILanguageContentService languageContentService)
{
    private record ScheduleMediaReference(
        string PublicationCode,
        string LanguageCode,
        string? SectionCode);

    public async Task FetchMissingAsync(string scheduleDbPath)
    {
        if (!File.Exists(scheduleDbPath))
        {
            logger.Debug("Schedule DB not found at {Path}, skipping schedule media bootstrap fetch", scheduleDbPath);
            return;
        }

        var references = await ReadNonEnglishReferencesAsync(scheduleDbPath);
        if (references.Count == 0)
        {
            logger.Information("No non-English schedule references found; skipping schedule media bootstrap fetch");
            return;
        }

        logger.Information(
            "Fetching missing non-English media for {Count} schedule reference(s) into new media index",
            references.Count);

        var publicationGroups = references
            .GroupBy(r => (r.PublicationCode, r.LanguageCode))
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
                    CancellationToken.None,
                    progress: null);

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
                        var sectionOk = await languageContentService.FetchSectionTracksAsync(
                            pubCode,
                            sectionCode,
                            langCode,
                            CancellationToken.None);

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

    private static async Task<List<ScheduleMediaReference>> ReadNonEnglishReferencesAsync(
        string scheduleDbPath)
    {
        var references = new List<ScheduleMediaReference>();
        using var connection = new SqliteConnection($"Data Source={scheduleDbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT PublicationCode, LanguageCode, SectionCode
            FROM BiblePublicationSchedules
            WHERE LanguageCode IS NOT NULL AND LanguageCode != @defaultLang
            UNION
            SELECT DISTINCT PublicationCode, LanguageCode, SectionCode
            FROM AlarmMusic
            WHERE LanguageCode IS NOT NULL AND LanguageCode != @defaultLang
            """;
        cmd.Parameters.AddWithValue("@defaultLang", AppConstants.Media.DefaultLanguageCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            references.Add(new ScheduleMediaReference(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return references;
    }
}
