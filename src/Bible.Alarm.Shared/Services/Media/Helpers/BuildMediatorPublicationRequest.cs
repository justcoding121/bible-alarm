#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parameters for <see cref="MediatorPublicationBuilder.BuildAndSavePublicationAsync"/>.
/// </summary>
internal readonly record struct BuildMediatorPublicationRequest(
    MediaDbContext Db,
    string PublicationCodeForDb,
    string? PublicationName,
    Language Language,
    List<BiblePublicationTrack> Tracks,
    CancellationToken CancellationToken);
