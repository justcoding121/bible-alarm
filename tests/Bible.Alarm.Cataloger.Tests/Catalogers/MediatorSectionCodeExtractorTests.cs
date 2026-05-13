#nullable enable

using Bible.Alarm.Cataloger.Catalogers;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class MediatorSectionCodeExtractorTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public void ExtractTracksFromMediatorCategory_returns_empty_when_category_property_is_absent()
    {
        var (tracks, localized) = MediatorSectionCodeExtractor.ExtractTracksFromMediatorCategory(
            "{}",
            publicationCode: "sjjm",
            languageCode: "E",
            SilentLogger);

        Assert.Empty(tracks);
        Assert.Null(localized);
    }
}
