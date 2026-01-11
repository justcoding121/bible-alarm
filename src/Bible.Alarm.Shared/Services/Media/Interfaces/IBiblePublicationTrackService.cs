#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing BiblePublicationTrack database operations.
/// </summary>
public interface IBiblePublicationTrackService : IDisposable
{
    /// <summary>
    /// Gets all BiblePublicationTracks for a given section (by language code, publication code, and section number), with Source included.
    /// </summary>
    Task<SortedDictionary<int, BiblePublicationTrack>> GetTracksBySectionAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a BiblePublicationTrack by language code, publication code, section number, and track number, with Source included.
    /// </summary>
    Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, int sectionNumber, int trackNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the URL for a Bible track's audio source.
    /// </summary>
    Task UpdateTrackUrlAsync(string languageCode, string publicationCode, int sectionNumber, int trackNumber, string url, CancellationToken cancellationToken = default);
}

