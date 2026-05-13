#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class KeyboardHelperTests
{
    [Fact]
    public void HideKeyboard_returns_when_entry_null()
    {
        KeyboardHelper.HideKeyboard(null);
    }
}
