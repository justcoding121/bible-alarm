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

    private async Task<PlayItem> GetNextMelodyTrackAsync(AlarmSchedule schedule, bool next)
    {
        var melodyMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        
        // IMPORTANT: Sectioned melody publications (e.g., "iam") have duplicate track numbers across discs.
        // Always select tracks from the schedule's selected disc (SectionCode) when sectioned.
        SortedDictionary<int, MusicTrack> melodyTracks;
        if (PublicationTypeHelper.HasSectionStructure(melodyMusic.PublicationCode))
        {
            if (string.IsNullOrWhiteSpace(melodyMusic.SectionCode))
            {
                melodyTracks = new SortedDictionary<int, MusicTrack>();
            }
            else
            {
                melodyTracks = await mediaService.GetMelodyMusicTracksBySection(melodyMusic.PublicationCode, melodyMusic.SectionCode);
            }
        }
        else
        {
            melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
        }

        var trackKey = GetNextTrackKey(melodyTracks, melodyMusic.TrackNumber, next);
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
        var vocalTracks = await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
        var trackKey = GetNextTrackKey(vocalTracks, vocalMusic.TrackNumber, next);
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
