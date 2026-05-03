#nullable enable

namespace Bible.Alarm.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void SyncfusionLicenseKey_is_nonempty_evaluated_token()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppSettings.SyncfusionLicenseKey));
        Assert.StartsWith("Ngo", AppSettings.SyncfusionLicenseKey, StringComparison.Ordinal);
    }
}
