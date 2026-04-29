#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for fetching and caching non-English language content on-demand.
/// When a user changes language, this service checks if the content exists in the database,
/// and if not, fetches it from the API and stores it for future use.
/// </summary>
public sealed class LanguageContentService : ILanguageContentService
{
    private readonly VideoLocalizedNameFetcher videoLocalizedNameFetcher;
    private readonly EnglishContentSeeder englishContentSeeder;
    private readonly PublicationEnsurer publicationEnsurer;
    private readonly LanguageContentPublicationTracksFetcher publicationTracksFetcher;
    private readonly LanguageContentPublicationSectionsFetcher publicationSectionsFetcher;
    private readonly LanguageContentSectionTracksFetcher sectionTracksFetcher;
    private readonly LanguageContentFirstSectionFetcher firstSectionFetcher;

    public LanguageContentService(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        HttpClient httpClient,
        IInternetConnectivityChecker? internetConnectivityChecker = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(httpClient);

        videoLocalizedNameFetcher = new VideoLocalizedNameFetcher(httpClient, logger);
        var mediatorFetcher = new MediatorFetcher(httpClient, logger);
        var flatPublicationFetcher = new FlatPublicationFetcher(httpClient, logger, videoLocalizedNameFetcher);
        var sectionFetcher = new SectionFetcher(httpClient, logger);
        englishContentSeeder = new EnglishContentSeeder(scopeFactory, httpClient, logger, mediatorFetcher, flatPublicationFetcher);
        publicationEnsurer = new PublicationEnsurer(scopeFactory, logger, this);
        publicationTracksFetcher = new LanguageContentPublicationTracksFetcher(scopeFactory, logger, mediatorFetcher, flatPublicationFetcher, internetConnectivityChecker);
        publicationSectionsFetcher = new LanguageContentPublicationSectionsFetcher(scopeFactory, logger, sectionFetcher, internetConnectivityChecker);
        sectionTracksFetcher = new LanguageContentSectionTracksFetcher(scopeFactory, logger, sectionFetcher, internetConnectivityChecker);
        firstSectionFetcher = new LanguageContentFirstSectionFetcher(scopeFactory, logger, this, sectionFetcher, internetConnectivityChecker);
    }

    public Task<string?> GetVideoPublicationDisplayNameAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        return videoLocalizedNameFetcher.FetchVideoLocalizedNameFromMediatorAsync(
            normalizedPublicationCode, normalizedLanguageCode, cancellationToken);
    }

    public async Task<bool> FetchPublicationTracksAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        return await publicationTracksFetcher.FetchPublicationTracksAsync(publicationCode, languageCode, cancellationToken);
    }

    public async Task<bool> FetchPublicationSectionsAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await publicationSectionsFetcher.FetchPublicationSectionsAsync(publicationCode, languageCode, progress, cancellationToken);
    }

    public async Task<bool> FetchSectionTracksAsync(
        string publicationCode,
        string sectionCode,
        string languageCode,
        bool replaceExistingTracksFromApi = false,
        CancellationToken cancellationToken = default)
    {
        return await sectionTracksFetcher.FetchSectionTracksAsync(
            publicationCode, sectionCode, languageCode, replaceExistingTracksFromApi, cancellationToken);
    }

    /// <summary>
    /// Fetches and creates English publication from scratch (for initial seeding).
    /// Determines category from publication code and uses discovered languages table.
    /// </summary>
    public async Task<bool> SeedEnglishPublicationAsync(
        string publicationCode,
        CancellationToken cancellationToken = default)
    {
        return await englishContentSeeder.SeedEnglishPublicationAsync(publicationCode, cancellationToken);
    }

    // Methods moved to EnglishContentSeeder helper class

    public async Task<bool> EnsurePublicationExistsAsync(
        string publicationCode,
        string languageCode,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await publicationEnsurer.EnsurePublicationExistsAsync(publicationCode, languageCode, progress, cancellationToken);
    }

    public async Task<bool> FetchFirstPublicationForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default)
    {
        return await publicationEnsurer.FetchFirstPublicationForLanguageAsync(languageCode, categoryName, cancellationToken);
    }

    public async Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await publicationEnsurer.EnsureAllPublicationsForLanguageAsync(languageCode, categoryName, progress, cancellationToken);
    }

    public async Task<bool> EnsureAllSectionsForPublicationAsync(
        string publicationCode,
        string languageCode,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await publicationEnsurer.EnsureAllSectionsForPublicationAsync(publicationCode, languageCode, progress, cancellationToken);
    }

    public async Task<bool> FetchFirstSectionOnlyAsync(
        string publicationCode,
        string firstSectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        return await firstSectionFetcher.FetchFirstSectionOnlyAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
    }

    // Method moved to VideoLocalizedNameFetcher helper class
}
