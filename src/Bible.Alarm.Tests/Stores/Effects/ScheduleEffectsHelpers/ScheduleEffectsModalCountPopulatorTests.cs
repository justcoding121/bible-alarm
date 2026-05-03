#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsModalCountPopulatorTests
{
    [Fact]
    public void AreModalCountsEquivalent_true_when_all_null()
    {
        var a = new ScheduleStateItem();
        var b = new ScheduleStateItem();

        Assert.True(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(a, b));
    }

    [Fact]
    public void AreModalCountsEquivalent_true_when_matching_counts()
    {
        var a = new ScheduleStateItem
        {
            BiblePublicationModalItemCount = 3,
            BiblePublicationSectionModalItemCount = 5,
            BiblePublicationTrackModalItemCount = 7,
            MusicPublicationModalItemCount = 2,
            MusicSectionModalItemCount = 9,
        };
        var b = new ScheduleStateItem
        {
            BiblePublicationModalItemCount = 3,
            BiblePublicationSectionModalItemCount = 5,
            BiblePublicationTrackModalItemCount = 7,
            MusicPublicationModalItemCount = 2,
            MusicSectionModalItemCount = 9,
        };

        Assert.True(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(a, b));
    }

    [Fact]
    public void AreModalCountsEquivalent_false_when_any_count_differs()
    {
        var baseCounts = new ScheduleStateItem
        {
            BiblePublicationModalItemCount = 1,
            BiblePublicationSectionModalItemCount = 1,
            BiblePublicationTrackModalItemCount = 1,
            MusicPublicationModalItemCount = 1,
            MusicSectionModalItemCount = 1,
        };

        Assert.False(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(baseCounts,
            new ScheduleStateItem
            {
                BiblePublicationModalItemCount = 2,
                BiblePublicationSectionModalItemCount = 1,
                BiblePublicationTrackModalItemCount = 1,
                MusicPublicationModalItemCount = 1,
                MusicSectionModalItemCount = 1,
            }));
        Assert.False(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(baseCounts,
            new ScheduleStateItem
            {
                BiblePublicationModalItemCount = 1,
                BiblePublicationSectionModalItemCount = 2,
                BiblePublicationTrackModalItemCount = 1,
                MusicPublicationModalItemCount = 1,
                MusicSectionModalItemCount = 1,
            }));
        Assert.False(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(baseCounts,
            new ScheduleStateItem
            {
                BiblePublicationModalItemCount = 1,
                BiblePublicationSectionModalItemCount = 1,
                BiblePublicationTrackModalItemCount = 2,
                MusicPublicationModalItemCount = 1,
                MusicSectionModalItemCount = 1,
            }));
        Assert.False(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(baseCounts,
            new ScheduleStateItem
            {
                BiblePublicationModalItemCount = 1,
                BiblePublicationSectionModalItemCount = 1,
                BiblePublicationTrackModalItemCount = 1,
                MusicPublicationModalItemCount = 2,
                MusicSectionModalItemCount = 1,
            }));
        Assert.False(ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(baseCounts,
            new ScheduleStateItem
            {
                BiblePublicationModalItemCount = 1,
                BiblePublicationSectionModalItemCount = 1,
                BiblePublicationTrackModalItemCount = 1,
                MusicPublicationModalItemCount = 1,
                MusicSectionModalItemCount = 2,
            }));
    }
}
