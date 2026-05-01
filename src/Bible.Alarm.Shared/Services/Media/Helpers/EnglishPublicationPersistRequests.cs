#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class EnglishPublicationUpdateRequest
{
    public required MediaDbContext Db { get; init; }
    public required BiblePublication ExistingPublication { get; init; }
    public required List<Category> Categories { get; init; }
    public required List<BiblePublicationSection> Sections { get; init; }
    public required string FinalPublicationName { get; init; }
    public required bool IsVideo { get; init; }
    public required bool IsBible { get; init; }
    public required bool PublicationWithoutLanguage { get; init; }
    public required string NormalizedPublicationCode { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed class EnglishPublicationInsertRequest
{
    public required MediaDbContext Db { get; init; }
    public required List<Category> Categories { get; init; }
    public required List<BiblePublicationSection> Sections { get; init; }
    public required string NormalizedPublicationCode { get; init; }
    public required string FinalPublicationName { get; init; }
    public Language? Language { get; init; }
    public int? LanguageId { get; init; }
    public required bool IsVideo { get; init; }
    public required bool IsBible { get; init; }
    public required bool PublicationWithoutLanguage { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
