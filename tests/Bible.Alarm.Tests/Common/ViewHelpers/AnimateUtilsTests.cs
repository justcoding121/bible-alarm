#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class AnimateUtilsTests
{
    [Fact]
    public void AnimateTouchFeedback_returns_without_animating_when_view_is_null()
    {
        AnimateUtils.AnimateTouchFeedback(null!);
    }
}
