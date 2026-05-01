#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for branching English seeding logic by <see cref="CatalogType"/>.
/// </summary>
internal readonly record struct SeedEnglishByCatalogTypeRequest(
    MediaDbContext Db,
    CatalogType CatalogType,
    string PublicationCode,
    string PublicationCodeForDb,
    string NormalizedPublicationCode,
    string NormalizedLanguageCode,
    Language Language,
    Category Category,
    string CategoryName,
    bool IsVideo,
    CancellationToken CancellationToken);
