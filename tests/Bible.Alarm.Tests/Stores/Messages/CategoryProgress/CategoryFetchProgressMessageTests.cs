#nullable enable

using Bible.Alarm.Stores.Messages.CategoryProgress;

namespace Bible.Alarm.Tests;

public sealed class CategoryFetchProgressMessageTests
{
    [Fact]
    public void Value_exposes_payload_from_constructor()
    {
        var payload = new CategoryFetchProgress
        {
            CategoryId = 7,
            Progress = 0.5,
            IsComplete = true,
            HasError = false,
        };

        var sut = new CategoryFetchProgressMessage(payload);

        Assert.Same(payload, sut.Value);
    }
}
