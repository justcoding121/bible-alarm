#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

public interface IBiblePublicationTrackService : IDisposable
{
    /// <summary>
    /// Gets all BiblePublicationTracks for a given section (by language code, publication code, and section code), with Source included.
    /// Pass null/empty sectionCode for non-sectioned publications.
    /// </summary>
    Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksBySectionAsync(string languageCode, string publicationCode, string? sectionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a BiblePublicationTrack by language code, publication code, section code, and track number, with Source included.
    /// Pass null/empty sectionCode for non-sectioned publications.
    /// </summary>
    Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, string? sectionCode, string trackCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the URL for a Bible track's audio source.
    /// </summary>
    Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string? sectionCode, string trackCode, string url, CancellationToken cancellationToken = default);
}

