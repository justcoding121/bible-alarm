#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="EnglishPublicationBuilder.BuildAndSavePublicationAsync"/>.
/// </summary>
internal readonly record struct BuildEnglishPublicationRequest(
    MediaDbContext Db,
    string NormalizedPublicationCode,
    string? PublicationName,
    Language? Language,
    bool IsVideo,
    bool IsBible,
    bool PublicationWithoutLanguage,
    List<BiblePublicationSection> Sections,
    CancellationToken CancellationToken);
