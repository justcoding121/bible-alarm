#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class AnimateUtilsTests
{
    [Fact]
    public void AnimateTouchFeedback_returns_when_view_reference_is_null_without_throwing()
    {
        var ex = Record.Exception(() => AnimateUtils.AnimateTouchFeedback(null!));

        Assert.Null(ex);
    }
}
