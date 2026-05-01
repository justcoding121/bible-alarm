#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal readonly record struct EnglishPublicationUpdateRequest(
    MediaDbContext Db,
    BiblePublication ExistingPublication,
    List<Category> Categories,
    List<BiblePublicationSection> Sections,
    string FinalPublicationName,
    bool IsVideo,
    bool IsBible,
    bool PublicationWithoutLanguage,
    string NormalizedPublicationCode,
    CancellationToken CancellationToken);

internal readonly record struct EnglishPublicationInsertRequest(
    MediaDbContext Db,
    List<Category> Categories,
    List<BiblePublicationSection> Sections,
    string NormalizedPublicationCode,
    string FinalPublicationName,
    Language? Language,
    int? LanguageId,
    bool IsVideo,
    bool IsBible,
    bool PublicationWithoutLanguage,
    CancellationToken CancellationToken);
