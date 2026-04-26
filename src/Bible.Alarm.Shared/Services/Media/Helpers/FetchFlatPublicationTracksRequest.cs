#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="FlatPublicationFetcher.FetchFlatPublicationTracksAsync"/>.
/// </summary>
internal readonly record struct FetchFlatPublicationTracksRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string NormalizedLanguageCode,
    BiblePublication EnglishPublication,
    bool IsVideo,
    bool IsMusic,
    string FileFormat,
    Language? Language,
    CancellationToken CancellationToken);
