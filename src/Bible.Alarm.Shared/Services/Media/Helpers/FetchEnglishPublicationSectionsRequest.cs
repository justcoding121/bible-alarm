#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="EnglishContentSeeder.FetchEnglishPublicationSectionsAsync"/>.
/// </summary>
internal readonly record struct FetchEnglishPublicationSectionsRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string NormalizedLanguageCode,
    Language? Language,
    string CategoryName,
    bool IsVideo,
    List<string> SectionCodes,
    CancellationToken CancellationToken);
