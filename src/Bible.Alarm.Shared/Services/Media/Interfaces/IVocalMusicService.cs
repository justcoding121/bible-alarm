#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing VocalMusic database operations.
/// </summary>
public interface IVocalMusicService : IDisposable
{
    /// <summary>
    /// Gets a VocalMusic release by language code and publication code.
    /// </summary>
    Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all VocalMusic releases for a given language code.
    /// </summary>
    Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all distinct Languages from VocalMusic.
    /// </summary>
    Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all tracks for a VocalMusic release by language code and publication code, with Source included.
    /// </summary>
    Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the URL for a VocalMusic track's audio source.
    /// </summary>
    Task UpdateTrackUrlAsync(string languageCode, string publicationCode, int trackNumber, string url, CancellationToken cancellationToken = default);
}

