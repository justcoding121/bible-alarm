#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class LookupKeysTests
{
    [Fact]
    public void CollectKeys_empty_schedules_yields_empty_key_sets()
    {
        var keys = LookupDataCollector.CollectKeys([]);

        Assert.Empty(keys.PublicationKeys);
        Assert.Empty(keys.SectionKeys);
        Assert.Empty(keys.BibleTrackKeys);
        Assert.Empty(keys.VocalMusicLanguageCodes);
        Assert.Empty(keys.VocalMusicKeys);
        Assert.Empty(keys.VocalTrackKeys);
        Assert.Empty(keys.MelodyPublicationCodes);
        Assert.Empty(keys.MelodySectionKeys);
    }

    [Fact]
    public void CollectKeys_extracts_bible_publication_and_melody_keys()
    {
        var schedule = new AlarmSchedule
        {
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "pub",
                LanguageCode = null,
                SectionCode = "  gen ",
                TrackCode = "1"
            },
            Music = new AlarmMusic
            {
                LanguageCode = null,
                PublicationCode = "mel",
                SectionCode = "s1",
                TrackCode = "t"
            }
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Contains((AppConstants.Media.DefaultLanguageCode, "pub"), keys.PublicationKeys);
        Assert.Contains((AppConstants.Media.DefaultLanguageCode, "pub", "gen"), keys.SectionKeys);
        Assert.Contains((AppConstants.Media.DefaultLanguageCode, "pub", "gen", "1"), keys.BibleTrackKeys);
        Assert.Contains("mel", keys.MelodyPublicationCodes);
        Assert.Contains(("mel", "s1"), keys.MelodySectionKeys);
    }
}
