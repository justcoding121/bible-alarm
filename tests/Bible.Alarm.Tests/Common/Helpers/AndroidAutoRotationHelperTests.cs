#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class AndroidAutoRotationHelperTests
{
    [Theory]
    [InlineData(-1, null)]
    [InlineData(-99, null)]
    [InlineData(0, 0)]
    [InlineData(42, 42)]
    public void ToNullableScheduleId_maps_negative_sentinel_to_null(int raw, int? expected) =>
        Assert.Equal(expected, AndroidAutoRotationHelper.ToNullableScheduleId(raw));

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(99, true)]
    public void ShouldPersistScheduleId_requires_positive_id(int? scheduleId, bool expected) =>
        Assert.Equal(expected, AndroidAutoRotationHelper.ShouldPersistScheduleId(scheduleId));

    [Fact]
    public void GetLastRotationScheduleId_and_SetLastRotationScheduleId_do_not_throw_on_headless_host()
    {
        var ex = Record.Exception(() =>
        {
            AndroidAutoRotationHelper.SetLastRotationScheduleId(12);
            _ = AndroidAutoRotationHelper.GetLastRotationScheduleId();
            AndroidAutoRotationHelper.SetLastRotationScheduleId(null);
        });

        Assert.Null(ex);
    }
}
