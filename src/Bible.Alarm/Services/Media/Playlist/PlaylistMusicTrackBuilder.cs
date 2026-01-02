#nullable enable
using Bible;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
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

    public PlaylistMusicTrackBuilder(
        ILogger logger,
        IMediaService mediaService,
        IMelodyMusicService melodyMusicService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.melodyMusicService = melodyMusicService;
    }

    public async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music?.MusicType == MusicType.Melodies)
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
        ValidateTrackSource(melodyTrack.Source, trackIndex, "Melody");
        return CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetNextVocalTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music ?? throw new InvalidOperationException("Music is null");
        var vocalTracks = await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
        var trackIndex = CalculateTrackIndex(vocalMusic.TrackNumber, vocalTracks.Count, next);
        var vocalTrack = vocalTracks[trackIndex];
        ValidateTrackSource(vocalTrack.Source, trackIndex, "Vocal");
        return CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int CalculateTrackIndex(int currentTrackNumber, int totalTracks, bool next)
    {
        if (next)
        {
            return (currentTrackNumber % totalTracks);
        }
        return currentTrackNumber - 1;
    }

    private static void ValidateTrackSource(AudioSource? source, int trackIndex, string trackType)
    {
        if (source == null)
        {
            throw new InvalidOperationException($"{trackType} track {trackIndex + 1} Source is null");
        }
    }

    private static PlayItem CreateMelodyPlayItem(AlarmSchedule schedule, AlarmMusic melodyMusic, MusicTrack melodyTrack)
    {
        if (melodyTrack.Source == null)
        {
            throw new InvalidOperationException($"Melody track {melodyTrack.Number} Source is null");
        }
        return new PlayItem(new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = melodyMusic.PublicationCode,
            TrackNumber = melodyTrack.Number,
            LookUpPath = melodyTrack.Source.LookUpPath
        }, melodyTrack.Source.Url);
    }

    private static PlayItem CreateVocalPlayItem(AlarmSchedule schedule, AlarmMusic vocalMusic, MusicTrack vocalTrack)
    {
        if (vocalTrack.Source == null)
        {
            throw new InvalidOperationException($"Vocal track {vocalTrack.Number} Source is null");
        }
        return new PlayItem(new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = vocalMusic.PublicationCode,
            LanguageCode = vocalMusic.LanguageCode,
            TrackNumber = vocalTrack.Number,
            LookUpPath = vocalTrack.Source.LookUpPath
        }, vocalTrack.Source.Url);
    }
}

