#nullable enable

using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceDependenciesTests
{
    [Fact]
    public void Struct_round_trips_all_dependency_slots()
    {
        var sut = new MediaServiceDependencies(
            null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.MediaIndexService);
        Assert.Null(sut.BiblePublicationService);
        Assert.Null(sut.ScopeFactory);
    }
}
