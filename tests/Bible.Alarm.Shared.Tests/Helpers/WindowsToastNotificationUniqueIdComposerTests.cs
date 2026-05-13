#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class WindowsToastNotificationUniqueIdComposerTests
{
    [Fact]
    public void Compose_keeps_full_suffix_when_under_platform_length_ceiling()
    {
        var fireDate = new DateTimeOffset(2028, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var id = WindowsToastNotificationUniqueIdComposer.Compose(12, fireDate);

        Assert.True(id.Length <= 16);
        Assert.StartsWith("12_", id, StringComparison.Ordinal);
        Assert.Equal("12_".Length + Sha256Base36CompactHasher.To11CharacterToken(fireDate).Length, id.Length);
    }

    [Fact]
    public void Compose_truncates_hash_when_schedule_digits_leave_no_room_for_full_suffix()
    {
        var fireDate = new DateTimeOffset(2031, 9, 8, 7, 6, 5, TimeSpan.Zero);

        var id = WindowsToastNotificationUniqueIdComposer.Compose(999_999_999, fireDate);

        Assert.Equal(16, id.Length);
        Assert.StartsWith("999999999_", id, StringComparison.Ordinal);
    }
}
