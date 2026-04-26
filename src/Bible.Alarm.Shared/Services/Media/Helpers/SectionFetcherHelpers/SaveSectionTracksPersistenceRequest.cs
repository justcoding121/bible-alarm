#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;

/// <summary>
/// Parameters for persisting section tracks with SQLite busy/unique-constraint retries.
/// </summary>
internal readonly record struct SaveSectionTracksPersistenceRequest(
    MediaDbContext Db,
    BiblePublicationSection Section,
    string SectionCode,
    string PublicationCode,
    string LanguageCode,
    CancellationToken CancellationToken);
