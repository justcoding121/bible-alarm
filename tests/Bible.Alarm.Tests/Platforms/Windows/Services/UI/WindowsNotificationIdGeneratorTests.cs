#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsNotificationIdGeneratorTests
{
    private static readonly DateTimeOffset SampleFire =
        new(2026, 5, 2, 14, 30, 45, TimeSpan.FromHours(-5));

    [Fact]
    public void BuildUniqueId_is_deterministic_for_same_inputs()
    {
        var a = WindowsNotificationIdGenerator.BuildUniqueId(7, SampleFire);
        var b = WindowsNotificationIdGenerator.BuildUniqueId(7, SampleFire);

        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildUniqueId_respects_windows_max_length_and_prefixes_schedule_id()
    {
        var smallId = WindowsNotificationIdGenerator.BuildUniqueId(3, SampleFire);

        Assert.True(smallId.Length <= 16);
        Assert.StartsWith("3_", smallId);
        Assert.Equal(13, smallId.Length);
    }

    [Fact]
    public void BuildUniqueId_truncates_hash_when_schedule_id_is_long()
    {
        var id = WindowsNotificationIdGenerator.BuildUniqueId(999_999, SampleFire);

        Assert.True(id.Length <= 16);
        Assert.StartsWith("999999_", id);
    }
}
