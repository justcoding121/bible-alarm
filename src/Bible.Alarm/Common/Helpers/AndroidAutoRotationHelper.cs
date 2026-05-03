#nullable enable
using Bible.Alarm.Common.Interfaces.Storage;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Persists the last schedule ID shown in Android Auto default-schedule rotation.
/// Used to cycle through schedules every 5 minutes when car is connected and not playing.
/// </summary>
public static class AndroidAutoRotationHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(AndroidAutoRotationHelper));

    private const string LastRotationScheduleIdKey = "AndroidAutoRotationLastScheduleId";

    private static IThreadSafePreferencesService? TryGetPreferencesService()
    {
        try
        {
            return ServiceProviderManager.GetService<IThreadSafePreferencesService>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static int? ToNullableScheduleId(int raw) => raw >= 0 ? raw : null;

    internal static bool ShouldPersistScheduleId(int? scheduleId) => scheduleId.HasValue && scheduleId.Value > 0;

    /// <summary>
    /// Gets the last schedule ID shown in Android Auto rotation, or null if none.
    /// </summary>
    public static int? GetLastRotationScheduleId()
    {
        try
        {
            var prefs = TryGetPreferencesService();
            var raw = prefs != null ? prefs.Get(LastRotationScheduleIdKey, -1) : Preferences.Get(LastRotationScheduleIdKey, -1);
            return ToNullableScheduleId(raw);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get Android Auto rotation last schedule ID");
            return null;
        }
    }

    /// <summary>
    /// Saves the schedule ID currently shown in Android Auto rotation.
    /// </summary>
    public static void SetLastRotationScheduleId(int? scheduleId)
    {
        try
        {
            var prefs = TryGetPreferencesService();
            if (ShouldPersistScheduleId(scheduleId))
            {
                var id = scheduleId!.Value;
                if (prefs != null)
                    prefs.Set(LastRotationScheduleIdKey, id);
                else
                    Preferences.Set(LastRotationScheduleIdKey, id);
            }
            else
            {
                if (prefs != null)
                    prefs.Remove(LastRotationScheduleIdKey);
                else
                    Preferences.Remove(LastRotationScheduleIdKey);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set Android Auto rotation last schedule ID");
        }
    }
}
