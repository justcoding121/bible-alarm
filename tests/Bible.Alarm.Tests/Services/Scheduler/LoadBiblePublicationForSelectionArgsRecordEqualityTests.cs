#nullable enable

using Bible.Alarm.Services.Scheduler;

namespace Bible.Alarm.Tests;

public sealed class LoadBiblePublicationForSelectionArgsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_field_values_are_equal()
    {
        var codes = new LoadBiblePublicationScheduleCodes(null, null, null, null);
        var a = new LoadBiblePublicationForSelectionArgs(
            ScheduleId: 0,
            IsNewSchedule: false,
            CurrentBiblePublication: null,
            Codes: codes,
            FinishedDuration: null);

        var b = new LoadBiblePublicationForSelectionArgs(
            a.ScheduleId,
            a.IsNewSchedule,
            a.CurrentBiblePublication,
            a.Codes,
            a.FinishedDuration);

        Assert.Equal(a, b);
    }
}
