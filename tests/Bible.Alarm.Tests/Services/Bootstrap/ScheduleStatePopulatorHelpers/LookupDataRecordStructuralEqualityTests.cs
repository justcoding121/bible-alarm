#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Tests;

public sealed class LookupDataRecordStructuralEqualityTests
{
    [Fact]
    public void LookupData_supports_with_expression_and_value_equality_for_empty_graph()
    {
        var empty = new LookupDataLoader.LookupData(
            new Dictionary<(string LanguageCode, string PublicationCode), BiblePublication>(),
            new Dictionary<(string LanguageCode, string PublicationCode, string SectionCode), string>(),
            new Dictionary<string, LookupDataLoader.NoLanguagePublicationMeta>(),
            new Dictionary<(string PublicationCode, string SectionCode), string>(),
            new Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string>(),
            new Dictionary<string, Language>(),
            new Dictionary<(string LanguageCode, string PublicationCode), VocalMusic>(),
            new Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>>(),
            new Dictionary<string, SortedDictionary<int, MusicTrack>>(),
            new Dictionary<(string PublicationCode, string SectionCode), SortedDictionary<int, MusicTrack>>(),
            new Dictionary<string, MelodyMusic>());

        var copy = empty with { };
        Assert.Equal(empty, copy);
    }
}
