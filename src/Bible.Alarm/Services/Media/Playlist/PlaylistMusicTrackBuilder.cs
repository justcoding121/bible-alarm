#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles building music tracks for playlists.
/// Separated from PlaylistService for better modularity.
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
    private readonly IUrlConstructionService? urlConstructionService;

    public PlaylistMusicTrackBuilder(
        ILogger logger,
        IMediaService mediaService,
        IMelodyMusicService melodyMusicService,
        IMediaUrlRefreshService urlRefreshService,
        IUrlConstructionService? urlConstructionService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.melodyMusicService = melodyMusicService;
        this.urlRefreshService = urlRefreshService;
        this.urlConstructionService = urlConstructionService;
    }

    public async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music?.MusicType == MusicType.Music)
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
        if (schedule.Music?.MusicType == MusicType.Music)
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

        var trackKey = GetNextTrackKey(melodyTracks, melodyMusic.TrackNumber, next);
        var melodyTrack = melodyTracks[trackKey];
        return await CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetPreviousMelodyTrackAsync(AlarmSchedule schedule)
    {
        var melodyMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        var melodyTracks = await GetMelodyTracksCachedAsync(melodyMusic);

        var trackKey = GetPreviousTrackKey(melodyTracks, melodyMusic.TrackNumber);
        var melodyTrack = melodyTracks[trackKey];
        return await CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetNextVocalTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        if (vocalMusic.LanguageCode == null)
        {
            throw new InvalidOperationException("LanguageCode is null for vocal music");
        }
        var vocalTracks = await GetVocalTracksCachedAsync(vocalMusic);
        var trackKey = GetNextTrackKey(vocalTracks, vocalMusic.TrackNumber, next);
        var vocalTrack = vocalTracks[trackKey];
        return await CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private async Task<PlayItem> GetPreviousVocalTrackAsync(AlarmSchedule schedule)
    {
        var vocalMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        if (vocalMusic.LanguageCode == null)
        {
            throw new InvalidOperationException("LanguageCode is null for vocal music");
        }

        var vocalTracks = await GetVocalTracksCachedAsync(vocalMusic);
        var trackKey = GetPreviousTrackKey(vocalTracks, vocalMusic.TrackNumber);
        var vocalTrack = vocalTracks[trackKey];
        return await CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int GetNextTrackKey(SortedDictionary<int, MusicTrack> tracks, int currentTrackNumber, bool next)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No tracks available");
        }

        if (!next)
        {
            return tracks.ContainsKey(currentTrackNumber) ? currentTrackNumber : tracks.Keys.First();
        }

        var keys = tracks.Keys.ToList();
        var currentIndex = keys.IndexOf(currentTrackNumber);
        if (currentIndex < 0)
        {
            return keys[0];
        }
        return keys[(currentIndex + 1) % keys.Count];
    }

    private static int GetPreviousTrackKey(SortedDictionary<int, MusicTrack> tracks, int currentTrackNumber)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No tracks available");
        }

        var keys = tracks.Keys.ToList();
        var currentIndex = keys.IndexOf(currentTrackNumber);
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
            TrackNumber = melodyTrack.Number,
            DownloadCode = melodyTrack.DownloadCode, // Store disc code (e.g., "iam-1", "iam-2") for melody music
            OriginalTrackNumber = melodyTrack.OriginalTrackNumber // Store original track number from API (within the disc)
            // LanguageCode is empty for melody music
        };

        // Use UrlConstructionService to get the lookup path from database.
        // For melodies, API expects pub=section (disc) code (e.g. iam-1), not publication code (iam).
        // Pass publication code + section/disc code so the DB track's UrlParams (pub=iam-1) are used.
        if (urlConstructionService != null && !string.IsNullOrEmpty(melodyMusic.PublicationCode) && !string.IsNullOrEmpty(melodyTrack.DownloadCode))
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                melodyMusic.PublicationCode,
                null, // Melodies don't have language
                melodyTrack.DownloadCode, // Section/disc code (e.g. "iam-1") - API uses this as pub=
                melodyTrack.Number);
            if (!string.IsNullOrEmpty(lookUpPath))
            {
                trackMetadata.LookUpPath = lookUpPath;
            }
        }

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
            TrackNumber = vocalTrack.Number,
            DownloadCode = vocalTrack.DownloadCode, // Typically same as publication code, but store for consistency
            OriginalTrackNumber = vocalTrack.OriginalTrackNumber // Typically same as Number for vocal music
        };

        // Use UrlConstructionService to get the lookup path from database
        if (urlConstructionService != null && !string.IsNullOrEmpty(vocalMusic.LanguageCode))
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                vocalMusic.PublicationCode,
                vocalMusic.LanguageCode,
                null, // No section for music
                vocalTrack.Number);
            if (!string.IsNullOrEmpty(lookUpPath))
            {
                trackMetadata.LookUpPath = lookUpPath;
            }
        }

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for vocal track {vocalTrack.Number}");
        }

        return new PlayItem(trackMetadata, url);
    }
}
