#nullable enable

using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionSelectionActionTests
{
    [Fact]
    public void CurrentBiblePublicationSchedule_echoes_constructor_row()
    {
        var dto = new BiblePublicationStateItem
        {
            Id = 3,
            LanguageCode = "E",
            PublicationCode = "nwt",
            TrackCode = "1",
            AlarmScheduleId = 99,
        };

        var action = new BiblePublicationSectionSelectionAction(dto);

        Assert.Same(dto, action.CurrentBiblePublicationSchedule);
    }
}
