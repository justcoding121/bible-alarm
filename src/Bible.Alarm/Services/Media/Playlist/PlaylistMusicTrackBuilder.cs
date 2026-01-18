#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
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
        var melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
        var trackIndex = CalculateTrackIndex(melodyMusic.TrackNumber, melodyTracks.Count, next);
        var melodyTrack = melodyTracks[trackIndex];
        return await CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetNextVocalTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        var vocalTracks = await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
        var trackIndex = CalculateTrackIndex(vocalMusic.TrackNumber, vocalTracks.Count, next);
        var vocalTrack = vocalTracks[trackIndex];
        return await CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int CalculateTrackIndex(int currentTrackNumber, int totalTracks, bool next)
    {
        if (next)
        {
            // Calculate next track number (wraps around if needed)
            // If at track 200, next is 1; if at track 9, next is 10
            var nextTrackNumber = ((currentTrackNumber) % totalTracks) + 1;
            return nextTrackNumber;
        }
        // Dictionary is keyed by track number (1-based), so use track number directly
        return currentTrackNumber;
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

        // Use UrlConstructionService to get the lookup path from database
        // For melodies, use DownloadCode (disc code) as publicationCode and null language
        if (urlConstructionService != null && !string.IsNullOrEmpty(melodyTrack.DownloadCode))
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                melodyTrack.DownloadCode, // Use disc code (e.g., "iam-1") as publication code
                null, // Melodies don't have language
                null, // No section for music
                melodyTrack.OriginalTrackNumber ?? melodyTrack.Number);
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
