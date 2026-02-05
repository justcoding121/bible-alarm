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
    private int lastScheduleCount = -1;
    private Dictionary<int, string> lastScheduleSignatures = new();
    private IState<ApplicationState>? applicationState;

    /// <summary>
    /// Initializes the tracker with the current schedule state.
    /// </summary>
    public void Initialize(IState<ApplicationState> applicationState)
    {
        this.applicationState = applicationState ?? throw new ArgumentNullException(nameof(applicationState));
        lastScheduleCount = applicationState.Value.Schedules?.Count ?? 0;
        lastScheduleSignatures = BuildScheduleSignatures(applicationState.Value.Schedules);
        logger.Debug("AndroidAutoScheduleChangeTracker initialized with {Count} schedules", lastScheduleCount);
    }

    /// <summary>
    /// Detects specific schedule changes (added, removed, or updated) and returns a list of changes.
    /// Returns null if no changes detected, or an empty list if changes detected but couldn't determine specifics.
    /// </summary>
    public List<ScheduleChange>? GetSpecificChanges()
    {
        if (applicationState?.Value?.Schedules == null)
        {
            return null;
        }

        var currentSchedules = applicationState.Value.Schedules;
        var currentScheduleCount = currentSchedules.Count;
        var currentSignatures = BuildScheduleSignatures(currentSchedules);

        // If count hasn't changed and signatures are equal, no changes
        if (lastScheduleCount == currentScheduleCount && AreSignaturesEqual(lastScheduleSignatures, currentSignatures))
        {
            logger.Debug("GetSpecificChanges: No changes detected (count: {Count}, signatures equal)", currentScheduleCount);
            return null;
        }

        logger.Debug("GetSpecificChanges: Changes detected (count: {OldCount} -> {NewCount})", lastScheduleCount, currentScheduleCount);

        var changes = new List<ScheduleChange>();

        // Find added schedules (in current but not in last)
        foreach (var schedule in currentSchedules)
        {
            if (!lastScheduleSignatures.ContainsKey(schedule.Id))
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
        foreach (var kvp in lastScheduleSignatures)
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
            if (lastScheduleSignatures.TryGetValue(kvp.Key, out var oldSignature) && oldSignature != kvp.Value)
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
                    logger.Information("Detected schedule updated: {ScheduleId} - Old signature: '{OldSignature}', New signature: '{NewSignature}'",
                        kvp.Key, oldSignature, kvp.Value);
                }
            }
        }

        // Update tracked state
        lastScheduleCount = currentScheduleCount;
        lastScheduleSignatures = currentSignatures;

        return changes;
    }

    /// <summary>
    /// Builds a signature dictionary from schedule collection.
    /// Each signature is a string representation of key properties that affect Android Auto display.
    /// 
    /// IMPORTANT: IsEnabled is intentionally excluded from the signature.
    /// This ensures Android Auto does NOT refresh when schedules are enabled/disabled on the home screen.
    /// Only changes to display-relevant properties (name, language, section, track, music) trigger a refresh.
    /// </summary>
    private static Dictionary<int, string> BuildScheduleSignatures(ICollection<ScheduleStateItem>? schedules)
    {
        var signatures = new Dictionary<int, string>();
        if (schedules == null)
        {
            return signatures;
        }

        foreach (var schedule in schedules)
        {
            var sectionCode = schedule.BiblePublicationSectionCode ?? string.Empty;
            // Create a signature from key properties that affect Android Auto display
            // Include MusicEnabled since it affects the icon shown in Android Auto
            // Include music track properties (MusicPublicationCode, MusicLanguageCode, MusicTrackNumber)
            // so that track navigation (next/prev) triggers a refresh
            // Note: MusicType is now inferred from MusicLanguageCode (null/empty = melody, otherwise = vocal)
            // Include BiblePublicationTrackTitle for dramas/videos where track title is shown
            // Include BiblePublicationName and BiblePublicationCategoryName since subtitle rendering depends on them
            // NOTE: IsEnabled is intentionally excluded - enabling/disabling schedules should NOT refresh Android Auto
            var signature = $"{schedule.Name}|{schedule.BiblePublicationCategoryName}|{schedule.BiblePublicationName}|{sectionCode}|{schedule.BiblePublicationTrackNumber}|{schedule.BiblePublicationLanguageName}|{schedule.BiblePublicationSectionName}|{schedule.BiblePublicationTrackTitle}|{schedule.MusicEnabled}|{schedule.MusicPublicationCode}|{schedule.MusicLanguageCode}|{schedule.MusicTrackNumber}";
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

