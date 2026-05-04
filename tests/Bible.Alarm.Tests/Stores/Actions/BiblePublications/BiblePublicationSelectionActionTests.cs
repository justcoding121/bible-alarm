#nullable enable

using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionActionTests
{
    [Fact]
    public void CurrentBiblePublicationSchedule_echoes_constructor_row()
    {
        var dto = new BiblePublicationStateItem
        {
            Id = 7,
            LanguageCode = "E",
            PublicationCode = "nwt",
            TrackCode = "2",
            AlarmScheduleId = 3,
        };

        var action = new BiblePublicationSelectionAction(dto);

        Assert.Same(dto, action.CurrentBiblePublicationSchedule);
    }
}
