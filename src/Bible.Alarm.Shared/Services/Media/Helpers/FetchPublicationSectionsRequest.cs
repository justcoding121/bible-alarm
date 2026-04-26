#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="SectionFetcher.FetchPublicationSectionsAsync"/> (keeps call sites under Sonar parameter limits).
/// </summary>
internal readonly record struct FetchPublicationSectionsRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string NormalizedLanguageCode,
    string PublicationCodeForDb,
    BiblePublication EnglishPublication,
    IReadOnlyList<string> SectionCodes,
    CancellationToken CancellationToken,
    IFetchProgress? Progress = null);
