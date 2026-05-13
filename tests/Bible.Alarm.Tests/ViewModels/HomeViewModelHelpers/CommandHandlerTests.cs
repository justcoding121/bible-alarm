#nullable enable

using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class CommandHandlerTests
{
    [Fact]
    public void Ctor_accepts_handlers()
    {
        var sut = new CommandHandler(
            null!,
            null!,
            null!,
            null!,
            _ => false,
            _ => Task.CompletedTask);
        Assert.NotNull(sut);
    }
}
