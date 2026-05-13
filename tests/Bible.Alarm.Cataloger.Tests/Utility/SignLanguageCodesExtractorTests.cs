#nullable enable

using System.Text.Json;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class SignLanguageCodesExtractorTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public void ExtractSignLanguageCodes_root_array_collects_only_entries_marked_sign_language()
    {
        var lc = AppConstants.Media.LanguageIndexJson.LangCode;
        var isl = AppConstants.Media.LanguageIndexJson.IsSignLanguage;
        var json = $$"""[{"{{lc}}":"sgn","{{isl}}":true},{"{{lc}}":"E","{{isl}}":false}]""";
        using var doc = JsonDocument.Parse(json);

        var codes = SignLanguageCodesExtractor.ExtractSignLanguageCodes(SilentLogger, doc);

        Assert.True(codes.SetEquals(["SGN"]));
    }

    [Fact]
    public void ExtractSignLanguageCodes_reads_array_nested_under_languages_key()
    {
        var lc = AppConstants.Media.LanguageIndexJson.LangCode;
        var isl = AppConstants.Media.LanguageIndexJson.IsSignLanguage;
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var json = $$"""
            {"{{lng}}":[{"{{lc}}":"ase","{{isl}}":true}]}
            """;
        using var doc = JsonDocument.Parse(json);

        var codes = SignLanguageCodesExtractor.ExtractSignLanguageCodes(SilentLogger, doc);

        Assert.True(codes.SetEquals(["ASE"]));
    }

    [Fact]
    public void ExtractSignLanguageCodes_reads_array_nested_under_data_key()
    {
        var lc = AppConstants.Media.LanguageIndexJson.LangCode;
        var isl = AppConstants.Media.LanguageIndexJson.IsSignLanguage;
        var data = AppConstants.Media.LanguageIndexJson.Data;
        var json = $$"""
            {"{{data}}":[{"{{lc}}":"fsl","{{isl}}":true}]}
            """;
        using var doc = JsonDocument.Parse(json);

        var codes = SignLanguageCodesExtractor.ExtractSignLanguageCodes(SilentLogger, doc);

        Assert.True(codes.SetEquals(["FSL"]));
    }

    [Fact]
    public void ExtractSignLanguageCodes_returns_empty_when_object_has_no_language_array()
    {
        using var doc = JsonDocument.Parse("""{"metadata":[]}""");

        var codes = SignLanguageCodesExtractor.ExtractSignLanguageCodes(SilentLogger, doc);

        Assert.Empty(codes);
    }

    [Fact]
    public void ExtractSignLanguageCodes_returns_empty_when_root_is_not_object_or_array()
    {
        using var doc = JsonDocument.Parse("42");

        var codes = SignLanguageCodesExtractor.ExtractSignLanguageCodes(SilentLogger, doc);

        Assert.Empty(codes);
    }
}
