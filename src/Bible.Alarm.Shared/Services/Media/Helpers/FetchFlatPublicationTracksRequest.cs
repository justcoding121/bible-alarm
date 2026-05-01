#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="FlatPublicationFetcher.FetchFlatPublicationTracksAsync"/>.
/// </summary>
internal sealed class FetchFlatPublicationTracksRequest
{
    public required MediaDbContext Db { get; init; }
    public required string NormalizedPublicationCode { get; init; }
    public required string NormalizedLanguageCode { get; init; }
    public required BiblePublication EnglishPublication { get; init; }
    public required bool IsVideo { get; init; }
    public required bool IsMusic { get; init; }
    public required string FileFormat { get; init; }
    public Language? Language { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
