#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class AnimateUtilsTests
{
    [Fact]
    public void AnimateTouchFeedback_with_null_view_returns_immediately()
    {
        AnimateUtils.AnimateTouchFeedback(null!);
    }
}
