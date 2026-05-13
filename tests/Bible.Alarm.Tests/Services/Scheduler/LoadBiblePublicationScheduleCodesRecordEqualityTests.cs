#nullable enable

using Bible.Alarm.Services.Scheduler;

namespace Bible.Alarm.Tests;

public sealed class LoadBiblePublicationScheduleCodesRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_field_values_are_equal()
    {
        var a = new LoadBiblePublicationScheduleCodes(
            LanguageCode: null,
            PublicationCode: null,
            SectionCode: null,
            TrackCode: null);

        var b = new LoadBiblePublicationScheduleCodes(
            a.LanguageCode,
            a.PublicationCode,
            a.SectionCode,
            a.TrackCode);

        Assert.Equal(a, b);
    }
}
