#nullable enable

using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class CategorySelectionActionTests
{
    [Fact]
    public void Constructor_sets_members_including_optional_snapshot()
    {
        var snap = new ScheduleStateItem { Id = 7 };
        var sut = new CategorySelectionAction(3, "Bible", previousLanguageCode: "E", previousScheduleSnapshot: snap);

        Assert.Equal(3, sut.CategoryId);
        Assert.Equal("Bible", sut.CategoryName);
        Assert.Equal("E", sut.PreviousLanguageCode);
        Assert.Same(snap, sut.PreviousScheduleSnapshot);
    }
}
