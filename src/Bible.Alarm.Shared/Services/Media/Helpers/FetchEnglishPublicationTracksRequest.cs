#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="EnglishContentSeeder.FetchEnglishPublicationTracksAsync"/>.
/// </summary>
internal readonly record struct FetchEnglishPublicationTracksRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string NormalizedLanguageCode,
    Language Language,
    Category Category,
    string CategoryName,
    bool IsVideo,
    CancellationToken CancellationToken);
