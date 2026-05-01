#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="SectionFetcher.FetchSectionTracksAsync"/> and
/// <see cref="SectionFetcherHelpers.SectionFetcherSectionTracksLoader.FetchSectionTracksAsync"/>.
/// </summary>
internal sealed class FetchSectionTracksRequest
{
    public required MediaDbContext Db { get; init; }
    public required string NormalizedPublicationCode { get; init; }
    public required string NormalizedSectionCode { get; init; }
    public required string NormalizedLanguageCode { get; init; }
    public required string PublicationCodeForDb { get; init; }
    public required BiblePublication Publication { get; init; }
    public required BiblePublicationSection Section { get; init; }
    public required CancellationToken CancellationToken { get; init; }
    public bool ReplaceExisting { get; init; }
}
