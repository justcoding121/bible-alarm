#nullable enable

using Bible.Alarm.Stores;

namespace Bible.Alarm.Tests;

public sealed class ReduxContainerTests
{
    [Fact]
    public void Store_set_and_get_round_trips()
    {
        var prior = ReduxContainer.Store;
        try
        {
            ReduxContainer.Store = null!;
            Assert.Null(ReduxContainer.Store);
        }
        finally
        {
            ReduxContainer.Store = prior;
        }
    }
}
