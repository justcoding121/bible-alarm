#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class KeyboardHelperTests
{
    [Fact]
    public void HideKeyboard_null_entry_is_no_op()
    {
        KeyboardHelper.HideKeyboard(null);
    }
}
