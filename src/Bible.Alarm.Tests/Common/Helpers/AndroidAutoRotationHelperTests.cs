#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class AndroidAutoRotationHelperTests
{
    [Theory]
    [InlineData(-2, null)]
    [InlineData(-1, null)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void ToNullableScheduleId_MapsRawPreferenceValue(int raw, int? expected) =>
        Assert.Equal(expected, AndroidAutoRotationHelper.ToNullableScheduleId(raw));

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void ShouldPersistScheduleId_OnlyPositiveIds(int? id, bool expected) =>
        Assert.Equal(expected, AndroidAutoRotationHelper.ShouldPersistScheduleId(id));
}
