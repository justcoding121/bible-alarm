#nullable enable

using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Services.Battery;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class BatteryOptimizationServiceTests
{
    private const string PromptKey = "AndroidBatteryOptimizationExclusionPromptShown";

    private sealed class FakeGeneralSettings : IGeneralSettingsService
    {
        public HashSet<string> ExistingKeys { get; } = [];

        public List<(string Key, string Value)> SetCalls { get; } = [];

        public bool ThrowOnExists { get; set; }

        public bool ThrowOnSet { get; set; }

        public void Dispose()
        {
        }

        public Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<GeneralSettings?>(null);

        public Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSet)
            {
                throw new InvalidOperationException("set failed");
            }

            SetCalls.Add((key, value));
            ExistingKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (ThrowOnExists)
            {
                throw new InvalidOperationException("exists failed");
            }

            return Task.FromResult(ExistingKeys.Contains(key));
        }
    }

    private sealed class FakeBatteryManager : IBatteryOptimizationManager
    {
        public int ShowOptimizationCalls { get; private set; }

        public int ShowDndCalls { get; private set; }

        public bool CanShowOptimizeActivityResult { get; init; } = true;

        public bool NotificationPolicyGranted { get; init; } = true;

        public bool IgnoringBattery { get; init; } = true;

        public void Dispose()
        {
        }

        public void ShowBatteryOptimizationExclusionSettingsPage() => ShowOptimizationCalls++;

        public bool CanShowOptimizeActivity() => CanShowOptimizeActivityResult;

        public void ShowDoNotDisturbSettingsPage() => ShowDndCalls++;

        public bool IsNotificationPolicyAccessGranted() => NotificationPolicyGranted;

        public bool IsIgnoringBatteryOptimizations() => IgnoringBattery;
    }

    [Fact]
    public async Task ShouldShowModalAsync_true_when_prompt_key_missing()
    {
        var settings = new FakeGeneralSettings();
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        Assert.True(await sut.ShouldShowModalAsync());
    }

    [Fact]
    public async Task ShouldShowModalAsync_false_when_prompt_key_exists()
    {
        var settings = new FakeGeneralSettings();
        settings.ExistingKeys.Add(PromptKey);
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        Assert.False(await sut.ShouldShowModalAsync());
    }

    [Fact]
    public async Task ShouldShowModalAsync_returns_false_when_settings_errors()
    {
        var settings = new FakeGeneralSettings { ThrowOnExists = true };
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        Assert.False(await sut.ShouldShowModalAsync());
    }

    [Fact]
    public async Task MarkModalAsShownAsync_writes_setting_when_missing()
    {
        var settings = new FakeGeneralSettings();
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        await sut.MarkModalAsShownAsync();

        var call = Assert.Single(settings.SetCalls);
        Assert.Equal(PromptKey, call.Key);
        Assert.Equal("True", call.Value);
        Assert.Contains(PromptKey, settings.ExistingKeys);
    }

    [Fact]
    public async Task MarkModalAsShownAsync_skips_when_already_recorded()
    {
        var settings = new FakeGeneralSettings();
        settings.ExistingKeys.Add(PromptKey);
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        await sut.MarkModalAsShownAsync();

        Assert.Empty(settings.SetCalls);
    }

    [Fact]
    public async Task MarkModalAsShownAsync_swallows_set_errors()
    {
        var settings = new FakeGeneralSettings { ThrowOnSet = true };
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        await sut.MarkModalAsShownAsync();

        Assert.Empty(settings.SetCalls);
    }

    [Fact]
    public void Delegates_and_null_manager_defaults()
    {
        var settings = new FakeGeneralSettings();
        var manager = new FakeBatteryManager();
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, manager);

        sut.ShowOptimizationSettingsPage();
        sut.ShowDoNotDisturbSettingsPage();

        Assert.Equal(1, manager.ShowOptimizationCalls);
        Assert.Equal(1, manager.ShowDndCalls);
        Assert.True(sut.CanShowOptimizeActivity());
        Assert.True(sut.IsNotificationPolicyAccessGranted());
        Assert.True(sut.IsIgnoringBatteryOptimizations());
    }

    [Fact]
    public void Null_battery_manager_methods_are_safe()
    {
        var settings = new FakeGeneralSettings();
        using var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, batteryOptimizationManager: null!);

        sut.ShowOptimizationSettingsPage();
        sut.ShowDoNotDisturbSettingsPage();

        Assert.False(sut.CanShowOptimizeActivity());
        Assert.False(sut.IsNotificationPolicyAccessGranted());
        Assert.False(sut.IsIgnoringBatteryOptimizations());
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var settings = new FakeGeneralSettings();
        var sut = new BatteryOptimizationService(TestLogging.CreateLogger(), settings, new FakeBatteryManager());

        sut.Dispose();
        sut.Dispose();
    }
}
