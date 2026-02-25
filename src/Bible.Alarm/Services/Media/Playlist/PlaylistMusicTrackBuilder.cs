#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles building music tracks for playlists.
/// Separated from PlaylistService for better modularity.
/// Save: For non-language music we save the language code selected on the row (e.g. MY) to AlarmMusic.LanguageCode so state is preserved on view schedule (like Bible).
/// Playback: We identify no-language pub by publication (IsPublicationWithoutLanguageAsync), not by stored LanguageCode. If no-language → melody path (track lookup by pub/section only). If languaged → vocal path (uses stored LanguageCode for track lookup). Same pattern as Bible schedule playback.
/// </summary>
public class PlaylistMusicTrackBuilder
{
    private static readonly TimeSpan TracksCacheTtl = TimeSpan.FromSeconds(30);

    private readonly record struct MelodyTracksCacheKey(string PublicationCode, string? SectionCode);
    private readonly record struct VocalTracksCacheKey(string LanguageCode, string PublicationCode);

    private sealed class CacheEntry<T>(DateTimeOffset createdAt, T value)
    {
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public T Value { get; } = value;
    }

    private readonly Dictionary<MelodyTracksCacheKey, CacheEntry<SortedDictionary<int, MusicTrack>>> melodyTracksCache = new();
    private readonly Dictionary<VocalTracksCacheKey, CacheEntry<SortedDictionary<int, MusicTrack>>> vocalTracksCache = new();
    private readonly object cacheLock = new();

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IUrlConstructionService urlConstructionService;

    public PlaylistMusicTrackBuilder(
        ILogger logger,
        IMediaService mediaService,
        IMelodyMusicService melodyMusicService,
        IMediaUrlRefreshService urlRefreshService,
        IUrlConstructionService urlConstructionService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.melodyMusicService = melodyMusicService;
        this.urlRefreshService = urlRefreshService;
        this.urlConstructionService = urlConstructionService ?? throw new ArgumentNullException(nameof(urlConstructionService));
    }

    /// <summary>
    /// Determines if the music publication is no-language (melody/instrumental) by checking the publication,
    /// same as Bible container. Uses BiblePublications.LanguageId == null, not stored LanguageCode.
    /// </summary>
    private async Task<bool> IsNoLanguageMusicPublicationAsync(AlarmSchedule schedule)
    {
        if (schedule?.Music == null || string.IsNullOrWhiteSpace(schedule.Music.PublicationCode))
        {
            return false;
        }
        return await mediaService.IsPublicationWithoutLanguageAsync(schedule.Music.PublicationCode);
    }

    public async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (await IsNoLanguageMusicPublicationAsync(schedule))
        {
            return await GetNextMelodyTrackAsync(schedule, next);
        }
        else
        {
            return await GetNextVocalTrackAsync(schedule, next);
        }
    }

    public async Task<PlayItem> PreviousMusicUrlToPlay(AlarmSchedule schedule)
    {
        if (await IsNoLanguageMusicPublicationAsync(schedule))
        {
            return await GetPreviousMelodyTrackAsync(schedule);
        }

        return await GetPreviousVocalTrackAsync(schedule);
    }

    private async Task<SortedDictionary<int, MusicTrack>> GetMelodyTracksCachedAsync(AlarmMusic melodyMusic)
    {
        var key = new MelodyTracksCacheKey(
            melodyMusic.PublicationCode,
            string.IsNullOrWhiteSpace(melodyMusic.SectionCode) ? null : melodyMusic.SectionCode);
        var now = DateTimeOffset.UtcNow;

        lock (cacheLock)
        {
            if (melodyTracksCache.TryGetValue(key, out var entry) && now - entry.CreatedAt <= TracksCacheTtl)
            {
                return entry.Value;
            }
        }

        SortedDictionary<int, MusicTrack> tracks;
        if (PublicationTypeHelper.HasSectionStructure(melodyMusic.PublicationCode))
        {
            tracks = string.IsNullOrWhiteSpace(melodyMusic.SectionCode)
                ? new SortedDictionary<int, MusicTrack>()
                : await mediaService.GetMelodyMusicTracksBySection(melodyMusic.PublicationCode, melodyMusic.SectionCode);
        }
        else
        {
            tracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
        }

        lock (cacheLock)
        {
            melodyTracksCache[key] = new CacheEntry<SortedDictionary<int, MusicTrack>>(now, tracks);
        }

        return tracks;
    }

    private async Task<SortedDictionary<int, MusicTrack>> GetVocalTracksCachedAsync(AlarmMusic vocalMusic)
    {
        var languageCode = vocalMusic.LanguageCode ?? string.Empty;
        var key = new VocalTracksCacheKey(languageCode.ToUpperInvariant(), vocalMusic.PublicationCode);
        var now = DateTimeOffset.UtcNow;

        lock (cacheLock)
        {
            if (vocalTracksCache.TryGetValue(key, out var entry) && now - entry.CreatedAt <= TracksCacheTtl)
            {
                return entry.Value;
            }
        }

        var tracks = await mediaService.GetVocalMusicTracks(languageCode, vocalMusic.PublicationCode);
        lock (cacheLock)
        {
            vocalTracksCache[key] = new CacheEntry<SortedDictionary<int, MusicTrack>>(now, tracks);
        }

        return tracks;
    }

    private async Task<PlayItem> GetNextMelodyTrackAsync(AlarmSchedule schedule, bool next)
    {
        var melodyMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");

        // IMPORTANT: Sectioned melody publications (e.g., "iam") have duplicate track numbers across discs.
        // Always select tracks from the schedule's selected disc (SectionCode) when sectioned.
        var melodyTracks = await GetMelodyTracksCachedAsync(melodyMusic);

        var trackKey = GetNextTrackKey(melodyTracks, melodyMusic.TrackCode, next);
        var melodyTrack = melodyTracks[trackKey];
        return await CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetPreviousMelodyTrackAsync(AlarmSchedule schedule)
    {
        var melodyMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        var melodyTracks = await GetMelodyTracksCachedAsync(melodyMusic);

        var trackKey = GetPreviousTrackKey(melodyTracks, melodyMusic.TrackCode);
        var melodyTrack = melodyTracks[trackKey];
        return await CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetNextVocalTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        // Vocal music: we're here because publication has a language (IsNoLanguageMusicPublicationAsync routed melody elsewhere)
        var vocalTracks = await GetVocalTracksCachedAsync(vocalMusic);
        var trackKey = GetNextTrackKey(vocalTracks, vocalMusic.TrackCode, next);
        var vocalTrack = vocalTracks[trackKey];
        return await CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private async Task<PlayItem> GetPreviousVocalTrackAsync(AlarmSchedule schedule)
    {
        var vocalMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        // Vocal music: publication has a language (no-language routed to melody path)

        var vocalTracks = await GetVocalTracksCachedAsync(vocalMusic);
        var trackKey = GetPreviousTrackKey(vocalTracks, vocalMusic.TrackCode);
        var vocalTrack = vocalTracks[trackKey];
        return await CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int GetNextTrackKey(SortedDictionary<int, MusicTrack> tracks, string? currentTrackCode, bool next)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No tracks available");
        }

        var keys = tracks.Keys.ToList();
        var currentKey = MusicTrackLookupHelper.GetKeyByCode(tracks, currentTrackCode);
        if (!currentKey.HasValue)
        {
            return next ? keys[0] : keys[^1];
        }

        if (!next)
        {
            return currentKey.Value;
        }

        var currentIndex = keys.IndexOf(currentKey.Value);
        if (currentIndex < 0)
        {
            return keys[0];
        }
        return keys[(currentIndex + 1) % keys.Count];
    }

    private static int GetPreviousTrackKey(SortedDictionary<int, MusicTrack> tracks, string? currentTrackCode)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No tracks available");
        }

        var keys = tracks.Keys.ToList();
        var currentKey = MusicTrackLookupHelper.GetKeyByCode(tracks, currentTrackCode);
        if (!currentKey.HasValue)
        {
            return keys[^1];
        }

        var currentIndex = keys.IndexOf(currentKey.Value);
        if (currentIndex < 0)
        {
            return keys[^1];
        }

        return keys[(currentIndex - 1 + keys.Count) % keys.Count];
    }

    private async Task<PlayItem> CreateMelodyPlayItem(AlarmSchedule schedule, AlarmMusic melodyMusic, MusicTrack melodyTrack)
    {
        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = melodyMusic.PublicationCode,
            TrackCode = melodyTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DownloadCode = melodyTrack.DownloadCode, // Store disc code (e.g., "iam-1", "iam-2") for melody music
            OriginalTrackCode = melodyTrack.OriginalTrackCode // Store original track number from API (within the disc)
            // LanguageCode is empty for melody music
        };

        // Lookup path from media index only (we only play harvested tracks).
        // For melodies, DB has pub=section (disc) code (e.g. iam-1). Pass sectionCode = DownloadCode.
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            melodyMusic.PublicationCode,
            null, // Melodies don't have language
            melodyTrack.DownloadCode, // Section/disc code (e.g. "iam-1")
            melodyTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={melodyMusic.PublicationCode}, section={melodyTrack.DownloadCode}, track={melodyTrack.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for melody track {melodyTrack.Number}");
        }

        return new PlayItem(trackMetadata, url);
    }

    private async Task<PlayItem> CreateVocalPlayItem(AlarmSchedule schedule, AlarmMusic vocalMusic, MusicTrack vocalTrack)
    {
        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = vocalMusic.PublicationCode,
            LanguageCode = vocalMusic.LanguageCode ?? string.Empty,
            TrackCode = vocalTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DownloadCode = vocalTrack.DownloadCode, // Typically same as publication code, but store for consistency
            OriginalTrackCode = vocalTrack.OriginalTrackCode // Typically same as Number for vocal music
        };

        // Lookup path from media index only (we only play harvested tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            vocalMusic.PublicationCode,
            vocalMusic.LanguageCode,
            null, // No section for vocal music
            vocalTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={vocalMusic.PublicationCode}, lang={vocalMusic.LanguageCode}, track={vocalTrack.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for vocal track {vocalTrack.Number}");
        }

        return new PlayItem(trackMetadata, url);
    }
}
