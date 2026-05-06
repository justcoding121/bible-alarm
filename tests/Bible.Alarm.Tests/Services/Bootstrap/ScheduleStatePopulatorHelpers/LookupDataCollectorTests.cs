#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class LookupDataCollectorTests
{
    [Fact]
    public void CollectKeys_EmptySchedules_YieldsEmptyKeySets()
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
    public void CollectKeys_BiblePublication_UsesDefaultLanguage_When_LanguageBlank()
    {
        var schedule = new AlarmSchedule
        {
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwtsty",
                LanguageCode = " ",
                SectionCode = "1",
                TrackCode = "3",
            },
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Contains((AppConstants.Media.DefaultLanguageCode, "nwtsty"), keys.PublicationKeys);
        var normalizedSection = SectionCodeHelper.Normalize("1")!;
        Assert.Contains((AppConstants.Media.DefaultLanguageCode, "nwtsty", normalizedSection), keys.SectionKeys);
        Assert.Contains((AppConstants.Media.DefaultLanguageCode, "nwtsty", normalizedSection, "3"), keys.BibleTrackKeys);
    }

    [Fact]
    public void CollectKeys_VocalMusic_AddsLanguagePublicationAndTrackKeys()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                LanguageCode = "MY",
                PublicationCode = "voc2025",
                TrackCode = "10",
            },
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Contains("MY", keys.VocalMusicLanguageCodes);
        Assert.Contains(("MY", "voc2025"), keys.VocalMusicKeys);
        Assert.Contains(("MY", "voc2025"), keys.VocalTrackKeys);
    }

    [Fact]
    public void CollectKeys_MelodyMusic_AddsPublicationAndSectionKeys()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                LanguageCode = null,
                PublicationCode = "iam-1",
                SectionCode = "iam-1-5",
                TrackCode = "2",
            },
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Contains("iam-1", keys.MelodyPublicationCodes);
        var melodySection = SectionCodeHelper.Normalize("iam-1-5")!;
        Assert.Contains(("iam-1", melodySection), keys.MelodySectionKeys);
        Assert.Empty(keys.VocalMusicLanguageCodes);
        Assert.Empty(keys.VocalMusicKeys);
        Assert.Empty(keys.VocalTrackKeys);
    }

    [Fact]
    public void CollectKeys_BiblePublication_skips_when_publication_code_blank()
    {
        var schedule = new AlarmSchedule
        {
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "   ",
                LanguageCode = "E",
                SectionCode = "1",
                TrackCode = "2",
            },
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Empty(keys.PublicationKeys);
        Assert.Empty(keys.SectionKeys);
        Assert.Empty(keys.BibleTrackKeys);
    }

    [Fact]
    public void CollectKeys_VocalMusic_adds_only_language_when_publication_code_blank()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "",
                TrackCode = "10",
            },
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Contains("E", keys.VocalMusicLanguageCodes);
        Assert.Empty(keys.VocalMusicKeys);
        Assert.Empty(keys.VocalTrackKeys);
    }

    [Fact]
    public void CollectKeys_VocalMusic_adds_publication_keys_but_not_track_when_track_code_blank()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "voc2025",
                TrackCode = "",
            },
        };

        var keys = LookupDataCollector.CollectKeys([schedule]);

        Assert.Contains("E", keys.VocalMusicLanguageCodes);
        Assert.Contains(("E", "voc2025"), keys.VocalMusicKeys);
        Assert.Empty(keys.VocalTrackKeys);
    }
}
