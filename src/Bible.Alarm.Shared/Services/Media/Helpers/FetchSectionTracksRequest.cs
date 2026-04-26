#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="SectionFetcher.FetchSectionTracksAsync"/> and
/// <see cref="SectionFetcherHelpers.SectionFetcherSectionTracksLoader.FetchSectionTracksAsync"/>.
/// </summary>
internal readonly record struct FetchSectionTracksRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string NormalizedSectionCode,
    string NormalizedLanguageCode,
    string PublicationCodeForDb,
    BiblePublication Publication,
    BiblePublicationSection Section,
    CancellationToken CancellationToken,
    bool ReplaceExisting = false);
