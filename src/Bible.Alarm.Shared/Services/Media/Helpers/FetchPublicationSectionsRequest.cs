#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="SectionFetcher.FetchPublicationSectionsAsync"/>.
/// </summary>
internal sealed class FetchPublicationSectionsRequest
{
    public required MediaDbContext Db { get; init; }
    public required string NormalizedPublicationCode { get; init; }
    public required string NormalizedLanguageCode { get; init; }
    public required string PublicationCodeForDb { get; init; }
    public required BiblePublication EnglishPublication { get; init; }
    public required IReadOnlyList<string> SectionCodes { get; init; }
    public required CancellationToken CancellationToken { get; init; }
    public IFetchProgress? Progress { get; init; }
}
