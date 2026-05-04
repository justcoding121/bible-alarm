#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionStateChangeCallbacksTests
{
    [Fact]
    public void Delegates_wire_through_supplied_implementations()
    {
        BiblePublicationSchedule? cur = null;
        BiblePublicationSchedule? last = null;
        bool init = true;
        bool? busy = null;
        (string, string)? initArgs = null;
        var selectedCalls = 0;

        var sut = new BiblePublicationSectionStateChangeCallbacks(
            b => cur = b,
            b => last = b,
            () => init,
            b => busy = b,
            (a, c) => initArgs = (a, c),
            () => selectedCalls++);

        var row = new BiblePublicationSchedule
        {
            Id = 1,
            LanguageCode = "E",
            PublicationCode = "pub",
            TrackCode = "1",
            FinishedDuration = TimeSpan.Zero,
            AlarmScheduleId = 9,
        };

        sut.SetCurrent(row);
        Assert.Same(row, cur);

        sut.SetLastCurrent(row);
        Assert.Same(row, last);

        Assert.True(sut.GetInitComplete());

        sut.SetIsBusy(false);
        Assert.False(busy);

        sut.Initialize("lc", "pc");
        Assert.Equal(("lc", "pc"), initArgs);

        sut.SetSelectedSection();
        Assert.Equal(1, selectedCalls);
    }
}
