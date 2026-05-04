#nullable enable

namespace Bible.Alarm.Tests;

public sealed class AppSettingsTests
{
    private const string SyncfusionLicensePlaceholder = "{{SYNCFUSION_LICENSE_KEY}}";

    [Fact]
    public void SyncfusionLicenseKey_is_nonempty_placeholder_or_substituted_key()
    {
        var key = AppSettings.SyncfusionLicenseKey;
        Assert.False(string.IsNullOrWhiteSpace(key));

        // Fork PRs and CI often build without SYNCFUSION_LICENSE_KEY; source keeps the placeholder.
        // Release/deploy pipelines substitute the real key (typically base64 beginning with "Ngo").
        if (string.Equals(key, SyncfusionLicensePlaceholder, StringComparison.Ordinal))
            return;

        Assert.StartsWith("Ngo", key, StringComparison.Ordinal);
    }
}
