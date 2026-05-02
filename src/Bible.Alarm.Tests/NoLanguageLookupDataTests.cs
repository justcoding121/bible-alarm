#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers.LookupLoading;

namespace Bible.Alarm.Tests;

public sealed class NoLanguageLookupDataTests
{
    [Fact]
    public void Empty_singleton_has_empty_dictionaries()
    {
        var empty = NoLanguageLookupData.Empty;

        Assert.Empty(empty.Publications);
        Assert.Empty(empty.Sections);
        Assert.Empty(empty.TrackTitles);
    }

    [Fact]
    public void Empty_singleton_is_stable_reference()
    {
        Assert.Same(NoLanguageLookupData.Empty, NoLanguageLookupData.Empty);
    }
}
