#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

public interface IVocalMusicService : IDisposable
{
    Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);

    Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default);

    Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tracks for a VocalMusic release by language code and publication code, with Source included.
    /// </summary>
    Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);

    Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default);
}

