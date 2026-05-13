#nullable enable

using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

namespace Bible.Alarm.Tests;

public sealed class CategorySelectionAutoPopulateHandlerDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new CategorySelectionAutoPopulateHandlerDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new CategorySelectionAutoPopulateHandlerDeps(
            a.BiblePublicationService,
            a.MediaService,
            a.LanguageContentService,
            a.LanguageNameService,
            a.ItemSelector,
            a.State,
            a.ScopeFactory,
            a.Logger);

        Assert.Equal(a, b);
    }
}
