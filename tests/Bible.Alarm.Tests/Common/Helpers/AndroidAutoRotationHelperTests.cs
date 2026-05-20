#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Tests.Support;

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

    [Fact]
    public void SetLastRotationScheduleId_round_trips_when_maui_preferences_available()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        try
        {
            AndroidAutoRotationHelper.SetLastRotationScheduleId(88);
            Assert.Equal(88, AndroidAutoRotationHelper.GetLastRotationScheduleId());

            AndroidAutoRotationHelper.SetLastRotationScheduleId(null);
            Assert.Null(AndroidAutoRotationHelper.GetLastRotationScheduleId());

            AndroidAutoRotationHelper.SetLastRotationScheduleId(0);
            Assert.Null(AndroidAutoRotationHelper.GetLastRotationScheduleId());
        }
        finally
        {
            AndroidAutoRotationHelper.SetLastRotationScheduleId(null);
        }
    }

    [Collection("MauiUi")]
    public sealed class MauiAndroidAutoRotationTests(MauiUiFixture _)
    {
        [Fact]
        public void Get_and_set_use_thread_safe_preferences_when_maui_ready()
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            try
            {
                AndroidAutoRotationHelper.SetLastRotationScheduleId(55);
                Assert.Equal(55, AndroidAutoRotationHelper.GetLastRotationScheduleId());

                AndroidAutoRotationHelper.SetLastRotationScheduleId(null);
                Assert.Null(AndroidAutoRotationHelper.GetLastRotationScheduleId());
            }
            finally
            {
                AndroidAutoRotationHelper.SetLastRotationScheduleId(null);
            }
        }
    }

    private static bool TryBootstrapMauiAppForPreferences()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return true;
        }

        try
        {
            MauiAppHolder.CreateAndStore();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
