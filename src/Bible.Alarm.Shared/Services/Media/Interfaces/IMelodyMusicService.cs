#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

public interface IMelodyMusicService : IDisposable
{
    /// <summary>
    /// Gets a MelodyMusic release by publication code, with Tracks included.
    /// </summary>
    Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default);

    Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tracks for a MelodyMusic release by publication code, with Source included.
    /// </summary>
    Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets tracks for a specific section in a music publication (e.g., "iam-1" section in Kingdom Melodies).
    /// </summary>
    Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the URL for a MelodyMusic track's audio source.
    /// </summary>
    Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default);
}

