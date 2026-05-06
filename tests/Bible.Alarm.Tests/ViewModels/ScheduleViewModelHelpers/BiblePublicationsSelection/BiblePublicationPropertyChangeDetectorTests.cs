#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationPropertyChangeDetectorTests
{
    private sealed class MutableState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value { get; set; } = value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public string? ResolvedName { get; set; }

        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => ResolvedName;
    }

    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Tests must not create service scopes.");
    }

    private static ScheduleStateItem BaseSchedule() =>
        new()
        {
            Id = 1,
            Name = "S",
            BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight,
        };

    private static BiblePublicationPropertyChangeDetector CreateDetector(
        ScheduleStateItem schedule,
        StubCategoryNameService categories)
    {
        var state = new MutableState(new ApplicationState([], schedule));
        var provider = new BiblePublicationDisplayTextProvider(
            state,
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            categories,
            new UnusedScopeFactory());
        return new BiblePublicationPropertyChangeDetector(provider);
    }

    [Fact]
    public void DetectPropertyChanges_language_code_change_cascades_notifications()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryId = 1;
        schedule.BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible;
        schedule.BiblePublicationLanguageCode = "en";
        schedule.BiblePublicationCode = "nwt";
        schedule.BiblePublicationSectionCode = "gen";
        schedule.BiblePublicationTrackCode = "1";

        var categories = new StubCategoryNameService { ResolvedName = "Bible" };
        var detector = CreateDetector(schedule, categories);

        detector.Initialize(
            schedule.BiblePublicationCategoryId,
            schedule.BiblePublicationCategoryName,
            schedule.BiblePublicationLanguageCode,
            schedule.BiblePublicationCode,
            schedule.BiblePublicationSectionCode,
            schedule.BiblePublicationTrackCode);

        schedule.BiblePublicationLanguageCode = "fr";
        var info = detector.DetectPropertyChanges(schedule);

        Assert.True(info.NotifyLanguage);
        Assert.True(info.NotifyPublication);
        Assert.True(info.NotifySection);
        Assert.True(info.NotifyTrack);
        Assert.True(info.CascadeChangeOccurred);
        Assert.True(info.HasChanges);
    }

    [Fact]
    public void DetectPropertyChanges_publication_flip_to_drama_notifies_section_visibility()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryId = 1;
        schedule.BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible;
        schedule.BiblePublicationLanguageCode = "en";
        schedule.BiblePublicationCode = "nwt";
        schedule.BiblePublicationSectionCode = "gen";
        schedule.BiblePublicationTrackCode = "1";

        var categories = new StubCategoryNameService { ResolvedName = "Bible" };
        var detector = CreateDetector(schedule, categories);

        detector.Initialize(
            schedule.BiblePublicationCategoryId,
            schedule.BiblePublicationCategoryName,
            schedule.BiblePublicationLanguageCode,
            schedule.BiblePublicationCode,
            schedule.BiblePublicationSectionCode,
            schedule.BiblePublicationTrackCode);

        schedule.BiblePublicationCode = AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
        var info = detector.DetectPropertyChanges(schedule);

        Assert.True(info.NotifyIsSectionVisible);
        Assert.True(info.NotifyPublication);
        Assert.True(info.CascadeChangeOccurred);
        Assert.False(info.DisplayTextOnlyChanged);
    }

    [Fact]
    public void DetectPropertyChanges_category_resolved_display_change_without_id_or_name_sets_display_text_only()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryId = 2;
        schedule.BiblePublicationCategoryName = "bkc";
        schedule.BiblePublicationLanguageCode = "en";
        schedule.BiblePublicationCode = "nwt";
        schedule.BiblePublicationSectionCode = null;
        schedule.BiblePublicationTrackCode = "1";

        var categories = new StubCategoryNameService { ResolvedName = "Books" };
        var detector = CreateDetector(schedule, categories);

        detector.Initialize(
            schedule.BiblePublicationCategoryId,
            schedule.BiblePublicationCategoryName,
            schedule.BiblePublicationLanguageCode,
            schedule.BiblePublicationCode,
            schedule.BiblePublicationSectionCode,
            schedule.BiblePublicationTrackCode);

        categories.ResolvedName = "Libros";
        var info = detector.DetectPropertyChanges(schedule);

        Assert.True(info.DisplayTextOnlyChanged);
        Assert.True(info.CategoryDisplayChanged);
        Assert.False(info.CascadeChangeOccurred);
        Assert.True(info.HasChanges);
    }
}
