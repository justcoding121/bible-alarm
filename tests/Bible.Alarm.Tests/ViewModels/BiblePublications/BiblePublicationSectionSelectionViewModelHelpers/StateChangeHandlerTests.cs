#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class StateChangeHandlerTests
{
    [Fact]
    public void HandleStateChanged_returns_early_when_schedule_null()
    {
        var setCurrentCount = 0;
        var sut = new StateChangeHandler(
            TestLogging.CreateLogger(),
            new StateChangeHandler.Callbacks(
                _ => setCurrentCount++,
                _ => { },
                () => false,
                _ => { },
                (_, _) => { },
                () => { }));

        sut.HandleStateChanged(new ApplicationState());

        Assert.Equal(0, setCurrentCount);
    }

    [Fact]
    public void HandleStateChanged_returns_early_when_publication_code_empty()
    {
        var setCurrentCount = 0;
        var sut = new StateChangeHandler(
            TestLogging.CreateLogger(),
            new StateChangeHandler.Callbacks(
                _ => setCurrentCount++,
                _ => { },
                () => false,
                _ => { },
                (_, _) => { },
                () => { }));

        sut.HandleStateChanged(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { BiblePublicationLanguageCode = "E", BiblePublicationCode = null }
        });

        Assert.Equal(0, setCurrentCount);
    }
}
