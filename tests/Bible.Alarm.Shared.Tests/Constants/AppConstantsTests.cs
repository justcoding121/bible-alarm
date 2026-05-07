#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Tests;

public sealed class AppConstantsTests
{
    /// <summary>
    /// Loads <see cref="AppConstants.Media"/> static readonly catalogs so their field initializers
    /// are executed under coverage (GETPUB / flat MP3 helpers depend on these lists).
    /// </summary>
    [Fact]
    public void Media_static_readonly_catalog_arrays_are_initialized()
    {
        Assert.NotEmpty(AppConstants.Media.ApiMisleadingGoodNewsVideoPublicationNamePhrases);
        Assert.NotEmpty(AppConstants.Media.VocalMusicCatalogPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3BooksPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3YearbooksPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3BrochuresPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3ArticleSeriesPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.GetPubIssueParameterPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.GetPubSingleTrackNoParamPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.GetPubSingleTrackZeroPublicationCodes);
    }

    [Fact]
    public void ApiEndpoints_mirror_hosts_and_GETPUBMediator_base_urls_are_well_formed()
    {
        Assert.Equal("b.jw-cdn.org", AppConstants.ApiEndpoints.JwCdnHostB);
        Assert.Equal("app.jw-cdn.org", AppConstants.ApiEndpoints.JwCdnHostApp);

        foreach (var origin in new[] { AppConstants.ApiEndpoints.JwCdnOriginHttpsB, AppConstants.ApiEndpoints.JwCdnOriginHttpsApp })
        {
            Assert.StartsWith("https://", origin, StringComparison.Ordinal);
        }

        Assert.Equal(
            AppConstants.ApiEndpoints.JwCdnOriginHttpsB + AppConstants.ApiEndpoints.PubMediaApisGetPubMedialinksPath,
            AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl);

        Assert.Equal(2, AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls.Length);
        Assert.All(
            AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls,
            u => Assert.Contains("GETPUBMEDIALINKS", u, StringComparison.OrdinalIgnoreCase));

        Assert.EndsWith(
            AppConstants.ApiEndpoints.MediatorApisV1Path,
            AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl,
            StringComparison.Ordinal);

        Assert.Equal(2, AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls.Length);
        Assert.All(
            AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls,
            u => Assert.Contains("/apis/mediator/v1", u, StringComparison.Ordinal));

        Assert.Equal(
            AppConstants.ApiEndpoints.JwOrgPublicWebsiteHttpsOrigin + AppConstants.ApiEndpoints.JwOrgLanguagesListPath,
            AppConstants.ApiEndpoints.JwOrgLanguagesListUrl);
    }

    [Fact]
    public void Database_sqlite_auxiliary_suffix_list_and_schedule_media_formats()
    {
        Assert.Equal(
            new[]
            {
                AppConstants.Database.SqliteWalFileSuffix,
                AppConstants.Database.SqliteShmFileSuffix,
                AppConstants.Database.SqliteJournalFileSuffix,
            },
            AppConstants.Database.SqliteAuxiliaryFileSuffixes);

        Assert.Equal("Filename={0}", AppConstants.Database.ScheduleDatabaseConnectionStringFormat);
        Assert.Equal("Filename={0}", AppConstants.Database.MediaIndexDatabaseConnectionStringFormat);
        Assert.Equal("schedule.db", AppConstants.Database.ScheduleDatabaseFileName);
        Assert.Equal("mediaIndex.db", AppConstants.Database.MediaIndexDatabaseFileName);
    }

    [Fact]
    public void ReviewSettings_gate_values_match_product_expectations()
    {
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumAppOpens);
        Assert.Equal(10, AppConstants.ReviewSettings.MinimumDismissals);
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumDaysSinceFirstDismissal);
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumDaysSinceInstall);
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumDaysSinceFirstOpen);
        Assert.Equal(30, AppConstants.ReviewSettings.RetryAfterDaysWhenNotFinalized);
        Assert.Equal(5, AppConstants.ReviewSettings.MinimumMinutesBetweenCountedAppOpens);
    }
}
