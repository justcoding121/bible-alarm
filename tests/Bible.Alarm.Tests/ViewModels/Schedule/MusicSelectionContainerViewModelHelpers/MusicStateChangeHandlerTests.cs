#nullable enable

using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicStateChangeHandlerTests
{
    [Fact]
    public void Ctor_wires_services_and_collaborators()
    {
        var services = new MusicStateChangeHandlerServices(
            null!, null!, null!, null!, null!);
        var collaborators = new MusicStateChangeHandlerCollaborators(
            null!, null!, null!);

        var sut = new MusicStateChangeHandler(services, collaborators);
        Assert.NotNull(sut);
    }
}
