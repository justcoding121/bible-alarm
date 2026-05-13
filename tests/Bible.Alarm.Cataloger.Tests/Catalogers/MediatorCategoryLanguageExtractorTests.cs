#nullable enable

using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class MediatorCategoryLanguageExtractorTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public void ExtractLanguagesFromCategory_returns_empty_when_category_property_missing()
    {
        var languages = MediatorCategoryLanguageExtractor.ExtractLanguagesFromCategory("{}", SilentLogger);

        Assert.Empty(languages);
    }

    [Fact]
    public void ExtractLanguagesFromCategory_collects_uppercase_language_codes_from_media_array()
    {
        var json = $$"""
            {
              "{{AppConstants.Media.PubMediaJson.Category}}": {
                "{{AppConstants.Media.PubMediaJson.CategoryMedia}}": [
                  { "{{AppConstants.Media.PubMediaJson.AvailableLanguages}}": ["e", "S"] },
                  { "{{AppConstants.Media.PubMediaJson.AvailableLanguages}}": ["e", "de"] }
                ]
              }
            }
            """;

        var languages = MediatorCategoryLanguageExtractor.ExtractLanguagesFromCategory(json, SilentLogger);

        Assert.Equal(3, languages.Count);
        Assert.Contains("E", languages);
        Assert.Contains("S", languages);
        Assert.Contains("DE", languages);
    }
}
