#nullable enable
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper class to track schedule changes for Android Auto services.
/// Detects when schedules are added, removed, or their properties change.
/// </summary>
public class AndroidAutoScheduleChangeTracker
{
    private static readonly ILogger Logger = Log.ForContext<AndroidAutoScheduleChangeTracker>();
    private int _lastScheduleCount = -1;
    private Dictionary<int, string> _lastScheduleSignatures = new();
    private IState<ApplicationState>? _applicationState;

    /// <summary>
    /// Initializes the tracker with the current schedule state.
    /// </summary>
    public void Initialize(IState<ApplicationState> applicationState)
    {
        _applicationState = applicationState ?? throw new System.ArgumentNullException(nameof(applicationState));
        _lastScheduleCount = applicationState.Value.Schedules?.Count ?? 0;
        _lastScheduleSignatures = BuildScheduleSignatures(applicationState.Value.Schedules);
        Logger.Debug("AndroidAutoScheduleChangeTracker initialized with {Count} schedules", _lastScheduleCount);
    }

    /// <summary>
    /// Checks if schedules have changed (count or properties) and updates the tracked state.
    /// Returns true if changes were detected.
    /// </summary>
    public bool CheckForChanges()
    {
        if (_applicationState?.Value?.Schedules == null)
        {
            return false;
        }

        var currentScheduleCount = _applicationState.Value.Schedules.Count;
        var currentSignatures = BuildScheduleSignatures(_applicationState.Value.Schedules);

        // Check if count changed or if any schedule properties changed
        var countChanged = _lastScheduleCount != currentScheduleCount;
        var propertiesChanged = !AreSignaturesEqual(_lastScheduleSignatures, currentSignatures);

        if (countChanged || propertiesChanged)
        {
            Logger.Debug("Schedule list changed (count: {OldCount} -> {NewCount}, properties changed: {PropertiesChanged})",
                _lastScheduleCount, currentScheduleCount, propertiesChanged);

            _lastScheduleCount = currentScheduleCount;
            _lastScheduleSignatures = currentSignatures;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a signature dictionary from schedule collection.
    /// Each signature is a string representation of key properties that affect Android Auto display.
    /// </summary>
    private static Dictionary<int, string> BuildScheduleSignatures(System.Collections.Generic.ICollection<ScheduleStateItem>? schedules)
    {
        var signatures = new Dictionary<int, string>();
        if (schedules == null)
        {
            return signatures;
        }

        foreach (var schedule in schedules)
        {
            // Create a signature from key properties that affect Android Auto display
            var signature = $"{schedule.Name}|{schedule.BibleReadingLanguageCode}|{schedule.BibleReadingBookNumber}|{schedule.BibleReadingChapterNumber}|{schedule.TranslationName}|{schedule.BookName}";
            signatures[schedule.Id] = signature;
        }

        return signatures;
    }

    /// <summary>
    /// Compares two signature dictionaries to determine if they are equal.
    /// </summary>
    private static bool AreSignaturesEqual(Dictionary<int, string> oldSignatures, Dictionary<int, string> newSignatures)
    {
        if (oldSignatures.Count != newSignatures.Count)
        {
            return false;
        }

        foreach (var kvp in newSignatures)
        {
            if (!oldSignatures.TryGetValue(kvp.Key, out var oldValue) || oldValue != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }
}

