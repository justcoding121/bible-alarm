#nullable enable

using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicTests
{
    [Fact]
    public void Fresh_instance_has_string_defaults_and_optional_section_language()
    {
        var m = new AlarmMusic();

        Assert.Equal(string.Empty, m.PublicationCode);
        Assert.Equal(string.Empty, m.TrackCode);
        Assert.Null(m.LanguageCode);
        Assert.Null(m.SectionCode);
    }
}
