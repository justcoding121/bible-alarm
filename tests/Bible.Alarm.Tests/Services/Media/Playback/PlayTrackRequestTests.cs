#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlayTrackRequestTests
{
    [Fact]
    public void IsPreparingOrPlaying_GetPlaylist_SetIsPreparingTrack_and_PlayedKeys_delegate_surface()
    {
        var meta = new TrackMetadata
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "1",
            LookUpPath = "/t",
        };
        var track = new AudioPlayerTrack { PlayItem = new PlayItem(meta, "https://x") };

        var played = new HashSet<string>();
        var preparingFlags = new List<bool>();
        List<AudioPlayerTrack>? playlistRef = null;
        var isPreparingCount = 0;

        var sut = new PlayTrackRequest(
            Track: track,
            CurrentTrackIndex: 3,
            StartFromBeginning: true,
            CurrentScheduleId: 99,
            IsPreparingOrPlaying: () =>
            {
                isPreparingCount++;
                return isPreparingCount == 1;
            },
            GetPlaylist: () => playlistRef,
            SetIsPreparingTrack: v => preparingFlags.Add(v),
            PlayedBibleTrackKeys: played,
            CancellationToken: default);

        Assert.True(sut.IsPreparingOrPlaying());
        Assert.False(sut.IsPreparingOrPlaying());

        playlistRef = [track];
        Assert.Same(playlistRef, sut.GetPlaylist());

        sut.SetIsPreparingTrack(true);
        sut.SetIsPreparingTrack(false);
        Assert.Equal(new[] { true, false }, preparingFlags);

        played.Add("k1");
        Assert.Contains("k1", sut.PlayedBibleTrackKeys);

        Assert.Same(track, sut.Track);
        Assert.Equal(3, sut.CurrentTrackIndex);
        Assert.True(sut.StartFromBeginning);
        Assert.Equal(99, sut.CurrentScheduleId);
    }
}
