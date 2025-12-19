#nullable enable
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Represents a specific change to a schedule in the Android Auto list.
/// </summary>
public enum ScheduleChangeType
{
    Added,
    Removed,
    Updated
}

/// <summary>
/// Represents a specific schedule change detected by the tracker.
/// </summary>
public class ScheduleChange
{
    public ScheduleChangeType ChangeType { get; set; }
    public int ScheduleId { get; set; }
    public ScheduleStateItem? Schedule { get; set; }
}

/// <summary>
/// Helper class to track schedule changes for Android Auto services.
/// Detects when schedules are added, removed, or their properties change.
/// </summary>
public class AndroidAutoScheduleChangeTracker
{
    private static readonly ILogger logger = Log.ForContext<AndroidAutoScheduleChangeTracker>();
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
        logger.Debug("AndroidAutoScheduleChangeTracker initialized with {Count} schedules", _lastScheduleCount);
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
            logger.Debug("Schedule list changed (count: {OldCount} -> {NewCount}, properties changed: {PropertiesChanged})",
                _lastScheduleCount, currentScheduleCount, propertiesChanged);

            _lastScheduleCount = currentScheduleCount;
            _lastScheduleSignatures = currentSignatures;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Detects specific schedule changes (added, removed, or updated) and returns a list of changes.
    /// Returns null if no changes detected, or an empty list if changes detected but couldn't determine specifics.
    /// </summary>
    public List<ScheduleChange>? GetSpecificChanges()
    {
        if (_applicationState?.Value?.Schedules == null)
        {
            return null;
        }

        var currentSchedules = _applicationState.Value.Schedules;
        var currentScheduleCount = currentSchedules.Count;
        var currentSignatures = BuildScheduleSignatures(currentSchedules);

        // If count hasn't changed and signatures are equal, no changes
        if (_lastScheduleCount == currentScheduleCount && AreSignaturesEqual(_lastScheduleSignatures, currentSignatures))
        {
            return null;
        }

        var changes = new List<ScheduleChange>();

        // Find added schedules (in current but not in last)
        foreach (var schedule in currentSchedules)
        {
            if (!_lastScheduleSignatures.ContainsKey(schedule.Id))
            {
                changes.Add(new ScheduleChange
                {
                    ChangeType = ScheduleChangeType.Added,
                    ScheduleId = schedule.Id,
                    Schedule = schedule
                });
                logger.Debug("Detected schedule added: {ScheduleId}", schedule.Id);
            }
        }

        // Find removed schedules (in last but not in current)
        foreach (var kvp in _lastScheduleSignatures)
        {
            if (!currentSignatures.ContainsKey(kvp.Key))
            {
                changes.Add(new ScheduleChange
                {
                    ChangeType = ScheduleChangeType.Removed,
                    ScheduleId = kvp.Key,
                    Schedule = null
                });
                logger.Debug("Detected schedule removed: {ScheduleId}", kvp.Key);
            }
        }

        // Find updated schedules (in both but signature changed)
        foreach (var kvp in currentSignatures)
        {
            if (_lastScheduleSignatures.TryGetValue(kvp.Key, out var oldSignature) && oldSignature != kvp.Value)
            {
                var schedule = currentSchedules.FirstOrDefault(s => s.Id == kvp.Key);
                if (schedule != null)
                {
                    changes.Add(new ScheduleChange
                    {
                        ChangeType = ScheduleChangeType.Updated,
                        ScheduleId = kvp.Key,
                        Schedule = schedule
                    });
                    logger.Debug("Detected schedule updated: {ScheduleId}", kvp.Key);
                }
            }
        }

        // Update tracked state
        _lastScheduleCount = currentScheduleCount;
        _lastScheduleSignatures = currentSignatures;

        return changes;
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

