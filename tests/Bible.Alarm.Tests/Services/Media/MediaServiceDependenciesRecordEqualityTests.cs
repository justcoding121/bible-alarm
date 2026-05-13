#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceDependenciesRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MediaServiceDependencies(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new MediaServiceDependencies(
            a.MediaIndexService,
            a.BiblePublicationService,
            a.BiblePublicationSectionService,
            a.BiblePublicationTrackService,
            a.MelodyMusicService,
            a.VocalMusicService,
            a.LanguageContentService,
            a.ScopeFactory);

        Assert.Equal(a, b);
    }
}
