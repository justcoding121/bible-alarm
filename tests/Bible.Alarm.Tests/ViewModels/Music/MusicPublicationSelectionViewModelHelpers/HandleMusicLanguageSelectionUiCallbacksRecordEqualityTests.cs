#nullable enable

using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HandleMusicLanguageSelectionUiCallbacksRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new HandleMusicLanguageSelectionUiCallbacks(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new HandleMusicLanguageSelectionUiCallbacks(
            a.SetCurrentLanguage,
            a.UpdateSelectedLanguage,
            a.SetShowProgress,
            a.SetProgressPercent,
            a.SetProgressText,
            a.SetIsBusy);

        Assert.Equal(a, b);
    }
}
