#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers.LookupLoading;

namespace Bible.Alarm.Tests;

public sealed class NoLanguageLookupDataTests
{
    [Fact]
    public void Empty_exposes_three_case_insensitive_empty_dictionaries()
    {
        var empty = NoLanguageLookupData.Empty;

        Assert.Empty(empty.Publications);
        Assert.Empty(empty.Sections);
        Assert.Empty(empty.TrackTitles);

        Assert.Same(empty.Publications.Comparer, StringComparer.OrdinalIgnoreCase);
    }
}
