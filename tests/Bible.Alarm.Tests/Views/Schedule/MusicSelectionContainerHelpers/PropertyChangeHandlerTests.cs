#nullable enable

using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

namespace Bible.Alarm.Tests;

public sealed class PropertyChangeHandlerTests
{
    [Fact]
    public void Ctor_accepts_container_and_actions()
    {
        PropertyChangeHandler sut = null!;
        try
        {
            sut = new PropertyChangeHandler(
                null!,
                (_, _) => { },
                () => { });

            Assert.NotNull(sut);
            Assert.True(sut.IsInitialLoad);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
