#nullable enable

using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ProgressBarAnimatorTests
{
    [Fact]
    public void Dispose_can_be_called_twice_without_throwing()
    {
        var sut = new ProgressBarAnimator();

        sut.Dispose();
        sut.Dispose();
    }
}
