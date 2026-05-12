#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ReviewPromptServiceTests
{
    private sealed class MemoryGeneralSettings : IGeneralSettingsService
    {
        public Dictionary<string, string> Store { get; } = new(StringComparer.Ordinal);

        public void Dispose()
        {
        }

        public Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.ContainsKey(key));

        public Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!Store.TryGetValue(key, out var value))
            {
                return Task.FromResult<GeneralSettings?>(null);
            }

            return Task.FromResult<GeneralSettings?>(new GeneralSettings { Key = key, Value = value });
        }

        public Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Store[key] = value;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RecordAppOpenAsync_skips_increment_inside_cooldown_window()
    {
        var settings = new MemoryGeneralSettings();
        await settings.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewStateMigrated, "True");
        await settings.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewAppOpenCount, "4");
        await settings.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewLastCountedAppOpenAtUtc,
            DateTime.UtcNow.ToString("O"));
        var sut = new ReviewPromptService(TestLogging.CreateLogger(), settings);

        await sut.RecordAppOpenAsync();

        Assert.Equal("4", settings.Store[AppConstants.GeneralSettingsKeys.ReviewAppOpenCount]);
    }

    [Fact]
    public async Task RecordAppOpenAsync_increments_after_cooldown_window()
    {
        var settings = new MemoryGeneralSettings();
        await settings.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewStateMigrated, "True");
        await settings.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewAppOpenCount, "2");
        await settings.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewLastCountedAppOpenAtUtc,
            DateTime.UtcNow.AddMinutes(-(AppConstants.ReviewSettings.MinimumMinutesBetweenCountedAppOpens + 1)).ToString("O"));
        var sut = new ReviewPromptService(TestLogging.CreateLogger(), settings);

        await sut.RecordAppOpenAsync();

        Assert.Equal("3", settings.Store[AppConstants.GeneralSettingsKeys.ReviewAppOpenCount]);
    }
}
