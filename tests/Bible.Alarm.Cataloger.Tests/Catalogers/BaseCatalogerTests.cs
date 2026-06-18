#nullable enable

using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class BaseCatalogerTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    private sealed class BaseCatalogerTestHarness(ILogger logger, DownloadUtility downloadUtility)
        : BaseCataloger(logger, downloadUtility)
    {
        public static List<(string Code, string Name, string Direction)>? Parse(string json) =>
            ParseLanguageEntries(json);

        public static List<(string Code, string Name, string Direction)>? Filter(
            List<(string Code, string Name, string Direction)>? entries,
            bool isTestRun) =>
            FilterLanguageEntriesForTestRun(entries, isTestRun);

        public static bool ShouldSkip(string name) => ShouldSkipLanguage(name);

        public ILogger ExposedLogger => Logger;

        public DownloadUtility ExposedDownloadUtility => DownloadUtility;

        public static int ExposedMaxConcurrentLanguageDownloads => MaxConcurrentLanguageDownloads;

        public static HashSet<string> ExposedTestRunLanguageCodes => TestRunLanguageCodes;
    }

    [Fact]
    public void Constructor_assigns_logger_and_download_utility()
    {
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult("{}"));
        var harness = new BaseCatalogerTestHarness(SilentLogger, downloadUtility);

        Assert.Same(SilentLogger, harness.ExposedLogger);
        Assert.Same(downloadUtility, harness.ExposedDownloadUtility);
    }

    [Fact]
    public void MaxConcurrentLanguageDownloads_is_eight()
    {
        Assert.Equal(8, BaseCatalogerTestHarness.ExposedMaxConcurrentLanguageDownloads);
    }

    [Fact]
    public void TestRunLanguageCodes_includes_english_malayalam_and_arabic()
    {
        var codes = BaseCatalogerTestHarness.ExposedTestRunLanguageCodes;

        Assert.Contains(AppConstants.Media.DefaultLanguageCode, codes);
        Assert.Contains("MY", codes);
        Assert.Contains("A", codes);
        Assert.Equal(3, codes.Count);
    }

    [Fact]
    public void ParseLanguageEntries_returns_null_when_languages_property_missing()
    {
        Assert.Null(BaseCatalogerTestHarness.Parse("{}"));
    }

    [Fact]
    public void ParseLanguageEntries_returns_null_when_languages_is_scalar()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        Assert.Null(BaseCatalogerTestHarness.Parse($$"""{"{{lng}}":"not-an-array-or-object"}"""));
    }

    [Fact]
    public void ParseLanguageEntries_reads_array_entries_with_langcode_name_and_direction()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var lc = AppConstants.Media.LanguageIndexJson.LangCode;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var dir = AppConstants.Media.LanguageIndexJson.Direction;
        var json = $$"""
            {
              "{{lng}}": [
                { "{{lc}}": "e", "{{nm}}": "English", "{{dir}}": "ltr" },
                { "{{lc}}": "s", "{{nm}}": "Spanish&nbsp;Name", "{{dir}}": "rtl" }
              ]
            }
            """;

        var entries = BaseCatalogerTestHarness.Parse(json);

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);
        Assert.Equal(("E", "English", "ltr"), entries[0]);
        Assert.Equal(("S", "Spanish Name", "rtl"), entries[1]);
    }

    [Fact]
    public void ParseLanguageEntries_reads_array_entries_using_symbol_when_langcode_missing()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var sym = AppConstants.Media.LanguageIndexJson.Symbol;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var json = $$"""
            {"{{lng}}":[{"{{sym}}":"my","{{nm}}":"Malayalam"}]}
            """;

        var entries = BaseCatalogerTestHarness.Parse(json);

        Assert.NotNull(entries);
        Assert.Single(entries!);
        Assert.Equal(("MY", "Malayalam", AppConstants.Media.TextDirectionLeftToRight), entries[0]);
    }

    [Fact]
    public void ParseLanguageEntries_reads_object_form_and_skips_entries_without_name()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var json = $$"""
            {
              "{{lng}}": {
                "E": { "{{nm}}": "English" },
                "X": { "other": "ignored" }
              }
            }
            """;

        var entries = BaseCatalogerTestHarness.Parse(json);

        Assert.NotNull(entries);
        Assert.Single(entries!);
        Assert.Equal("E", entries![0].Code);
    }

    [Fact]
    public void ParseLanguageEntries_returns_null_when_no_valid_entries()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var json = $$"""{"{{lng}}":[{"missing":"fields"}]}""";

        Assert.Null(BaseCatalogerTestHarness.Parse(json));
    }

    [Fact]
    public void FilterLanguageEntriesForTestRun_returns_null_for_null_or_empty_input()
    {
        Assert.Null(BaseCatalogerTestHarness.Filter(null, isTestRun: false));
        Assert.Null(BaseCatalogerTestHarness.Filter([], isTestRun: true));
    }

    [Fact]
    public void FilterLanguageEntriesForTestRun_returns_all_entries_when_not_test_run()
    {
        var entries = new List<(string Code, string Name, string Direction)>
        {
            ("E", "English", "ltr"),
            ("DE", "German", "ltr"),
        };

        var filtered = BaseCatalogerTestHarness.Filter(entries, isTestRun: false);

        Assert.Same(entries, filtered);
    }

    [Fact]
    public void FilterLanguageEntriesForTestRun_keeps_only_test_language_codes()
    {
        var entries = new List<(string Code, string Name, string Direction)>
        {
            ("E", "English", "ltr"),
            ("MY", "Malayalam", "ltr"),
            ("DE", "German", "ltr"),
            ("A", "Arabic", "rtl"),
        };

        var filtered = BaseCatalogerTestHarness.Filter(entries, isTestRun: true);

        Assert.NotNull(filtered);
        Assert.Equal(3, filtered!.Count);
        Assert.DoesNotContain(filtered, e => e.Code == "DE");
    }

    [Fact]
    public void FilterLanguageEntriesForTestRun_returns_null_when_no_test_codes_match()
    {
        var entries = new List<(string Code, string Name, string Direction)>
        {
            ("DE", "German", "ltr"),
        };

        Assert.Null(BaseCatalogerTestHarness.Filter(entries, isTestRun: true));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("English", false)]
    [InlineData("American Sign Language", true)]
    public void ShouldSkipLanguage_detects_sign_language_names(string? languageName, bool expected)
    {
        Assert.Equal(expected, BaseCatalogerTestHarness.ShouldSkip(languageName!));
    }
}
