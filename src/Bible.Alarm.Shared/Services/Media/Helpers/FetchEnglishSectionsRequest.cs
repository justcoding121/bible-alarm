#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="EnglishSectionFetcher.FetchSectionsAsync(FetchEnglishSectionsRequest)"/>.
/// </summary>
internal readonly record struct FetchEnglishSectionsRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string NormalizedLanguageCode,
    string CategoryName,
    List<string> SectionCodes,
    bool IsVideo,
    CancellationToken CancellationToken);
