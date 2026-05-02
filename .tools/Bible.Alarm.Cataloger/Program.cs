#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using CatalogValidator = Bible.Alarm.Cataloger.Utility.CatalogValidator;
using DirectoryHelper = Bible.Alarm.Cataloger.Utility.DirectoryHelper;
using ILogger = Serilog.ILogger;

namespace Bible.Alarm.Cataloger;

public static class Program
{

    // Bible publication codes to catalog (from centralized JwSourceHelper)


    public static async Task<int> Main(string[] args)
    {
        var logging = ResolveLoggingFlags(args);
        InitializeSerilog(logging);

        var services = new ServiceCollection();
        RegisterCatalogerServices(services);

        await using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger>();

        var isTestRun = args.Contains("--test-run", StringComparer.OrdinalIgnoreCase) ||
                        args.Contains("--test-mode", StringComparer.OrdinalIgnoreCase) ||
                        args.Contains("--TestRun", StringComparer.OrdinalIgnoreCase);

        var publicationFilter = ParsePublicationFilter(args, logger, DirectoryHelper.IndexDirectory);
        if (publicationFilter != null)
        {
            logger.Information("=== PUBLICATION FILTER: Processing only {Count} publication(s) ===", publicationFilter.Count);
        }

        if (isTestRun)
        {
            logger.Information("=== TEST RUN MODE: Processing English (E), Malayalam (MY), and Arabic (A) languages per publication ===");
        }

        int pipelineExitCode;
        try
        {
            pipelineExitCode = await RunCatalogerPipelineAsync(serviceProvider, logger, publicationFilter, isTestRun);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during cataloging");
            pipelineExitCode = 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }

        return CompleteCatalogerMainAfterPipeline(logger, pipelineExitCode);
    }

    private static int CompleteCatalogerMainAfterPipeline(ILogger logger, int pipelineExitCode)
    {
        if (pipelineExitCode != 0) // NOSONAR S2583 — value comes from RunCatalogerPipelineAsync / catch, not constant
        {
            return pipelineExitCode;
        }

        var zipIndex = $"{DirectoryHelper.IndexDirectory}/{AppConstants.FilePaths.MediaIndexZipFileName}";
        if (!File.Exists(zipIndex))
        {
            logger.Error("Cataloging failed to create zip file.");
            return 1;
        }

        return 0;
    }

    private readonly record struct LoggingFlags(bool Verbose, bool Quiet);

    private static LoggingFlags ResolveLoggingFlags(string[] args)
    {
        var verbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase) ||
                      args.Contains("-v", StringComparer.OrdinalIgnoreCase);
        var quiet = args.Contains("--quiet", StringComparer.OrdinalIgnoreCase) ||
                    args.Contains("-q", StringComparer.OrdinalIgnoreCase);

        var logLevelEnv = Environment.GetEnvironmentVariable("CATALOGER_LOG_LEVEL");
        if (!string.IsNullOrWhiteSpace(logLevelEnv))
        {
            var level = logLevelEnv.Trim();
            if (level.Equals("Quiet", StringComparison.OrdinalIgnoreCase) || level.Equals("Minimal", StringComparison.OrdinalIgnoreCase))
            {
                quiet = true;
            }
            else if (level.Equals("Debug", StringComparison.OrdinalIgnoreCase) || level.Equals("Verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
            }
        }

        if (!quiet && string.Equals(Environment.GetEnvironmentVariable("CATALOGER_QUIET"), "1", StringComparison.OrdinalIgnoreCase))
        {
            quiet = true;
        }

        return new LoggingFlags(verbose, quiet);
    }

    private static void InitializeSerilog(LoggingFlags logging)
    {
        var minimumLevel = logging.Verbose ? LogEventLevel.Debug : LogEventLevel.Information;
        var catalogerLevel = logging.Quiet ? LogEventLevel.Warning : LogEventLevel.Information;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Bible.Alarm.Cataloger.Program", LogEventLevel.Information)
            .MinimumLevel.Override("Bible.Alarm.Cataloger.Catalogers", catalogerLevel)
            .MinimumLevel.Override("Bible.Alarm.Cataloger.Seeders", catalogerLevel)
            .MinimumLevel.Override("Bible.Alarm.Cataloger.Utility", catalogerLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void RegisterCatalogerServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSerilog(Log.Logger);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
        services.AddSingleton(_ => Log.Logger);

        services.AddDbContext<MediaDbContext>(options =>
        {
            var indexDir = DirectoryHelper.IndexDirectory;
            var dbDir = Path.Combine(new DirectoryInfo(indexDir).FullName, AppConstants.FilePaths.MediaIndexCatalogOutputDbDirectoryName);
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            var dbPath = Path.Combine(dbDir, AppConstants.Database.MediaIndexDatabaseFileName);
            var connectionString = $"Data Source={dbPath};";

            options.UseSqlite(connectionString, b =>
            {
                b.MigrationsAssembly("Bible.Alarm.Shared");
                b.CommandTimeout(60);
                b.UseQuerySplittingBehavior(Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery);
            });
        });

        services.AddTransient<BibleCataloger>();
        services.AddTransient<MusicCataloger>();
        services.AddTransient<MediatorCataloger>();
        services.AddTransient<VideoCataloger>();
        services.AddTransient<DbSeeder>();
        services.AddTransient<DownloadUtility>();
        services.AddSingleton<System.Net.Http.HttpClient>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationService, Bible.Alarm.Shared.Services.Media.BiblePublicationService>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService, Bible.Alarm.Shared.Services.Media.LanguageContentService>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationSectionService, Bible.Alarm.Shared.Services.Media.BiblePublicationSectionService>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationTrackService, Bible.Alarm.Shared.Services.Media.BiblePublicationTrackService>();
    }

    private static async Task<int> RunCatalogerPipelineAsync(
        ServiceProvider serviceProvider,
        ILogger logger,
        HashSet<string>? publicationFilter,
        bool isTestRun)
    {
        var indexZipPath = $"{DirectoryHelper.IndexDirectory}/{AppConstants.FilePaths.MediaIndexZipFileName}";
        var indexZipFile = new FileInfo(indexZipPath);
        var originalIndexFileSize = indexZipFile.Exists ? indexZipFile.Length : 0;

        DeleteDirectory(DirectoryHelper.IndexDirectory);

        var languageCodeToInfoMappings = new ConcurrentDictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var downloadUtility = serviceProvider.GetRequiredService<DownloadUtility>();
        var dbSeederLogger = serviceProvider.GetRequiredService<ILogger>();
        var failedListPath = Path.Combine(DirectoryHelper.IndexDirectory, AppConstants.FilePaths.CatalogerLastRunFailedListFileName);
        IDataPersister dataPersister = new DbSeeder(dbSeederLogger, scopeFactory, downloadUtility, isTestRun, failedListPath);

        await using (var catalogerScope = serviceProvider.CreateAsyncScope())
        {
            var phaseExit = await RunDiscoveryAndCatalogPhasesAsync(
                catalogerScope.ServiceProvider,
                logger,
                dataPersister,
                languageCodeToInfoMappings,
                languageCodeToEditionsMapping,
                publicationFilter,
                isTestRun);
            if (phaseExit != 0)
            {
                return phaseExit;
            }
        }

        if (dataPersister is not DbSeeder dbSeeder)
        {
            logger.Error("dataPersister is not a DbSeeder instance. Cannot seed database.");
            return 1;
        }

        var seedExit = await RunSeedDatabaseAsync(dbSeeder, logger, publicationFilter, isTestRun);
        if (seedExit != 0)
        {
            return seedExit;
        }

        var validationExit = await RunCatalogValidatorAsync(serviceProvider, logger, publicationFilter);
        if (validationExit != 0)
        {
            return validationExit;
        }

        return FinalizeIndexZip(logger, originalIndexFileSize, isTestRun);
    }

    private static async Task<int> RunDiscoveryAndCatalogPhasesAsync(
        IServiceProvider catalogerServices,
        ILogger logger,
        IDataPersister dataPersister,
        ConcurrentDictionary<string, LanguageInfo> languageCodeToInfoMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping,
        HashSet<string>? publicationFilter,
        bool isTestRun)
    {
        var catalogerLogger = catalogerServices.GetRequiredService<ILogger>();
        var catalogerDownloadUtility = catalogerServices.GetRequiredService<DownloadUtility>();

        var bibleCataloger = new BibleCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
        var musicCataloger = new MusicCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
        var mediatorCataloger = new MediatorCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
        var videoCataloger = new VideoCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
        var magazineCataloger = new MagazineCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);

        var biblePublicationCodeToNameMappings = JwSourceHelper.BiblePublicationCodes.ToDictionary(
            code => code,
            code => code,
            StringComparer.OrdinalIgnoreCase);

        logger.Information("=== PHASE 1: DISCOVERY ===");
        await bibleCataloger.DiscoverLanguages(biblePublicationCodeToNameMappings, isTestRun);

        if (publicationFilter == null ||
            publicationFilter.Overlaps(JwSourceHelper.WatchtowerMagazinePublicationCodes) ||
            publicationFilter.Overlaps(JwSourceHelper.AwakeMagazinePublicationCodes))
        {
            await magazineCataloger.DiscoverLanguagesForMagazines(isTestRun);
        }

        logger.Debug("=== DISCOVERY PHASE COMPLETED ===\n");

        var mediatorLinksValid = await CatalogValidator.ValidateMediatorLinksAsync(catalogerLogger);
        if (!mediatorLinksValid)
        {
            logger.Error("Mediator link validation failed: one or more category URLs did not return valid category.media. Failing cataloger.");
            return 1;
        }

        logger.Information("=== PHASE 2: CATALOGING ===");
        var bibleTasks = new List<Task>();
        if (publicationFilter == null || publicationFilter.Overlaps(JwSourceHelper.BiblePublicationCodes))
        {
            bibleTasks.Add(bibleCataloger.CatalogBibleLinks(biblePublicationCodeToNameMappings, languageCodeToInfoMappings, languageCodeToEditionsMapping, isTestRun));
        }

        var musicTasks = new List<Task>
        {
            musicCataloger.CatalogVocalMusicLinks(isTestRun, publicationFilter),
            musicCataloger.CatalogMusicMelodyLinks(isTestRun, publicationFilter),
            musicCataloger.CatalogArticleSeriesLinks(isTestRun, publicationFilter),
            musicCataloger.CatalogBooksLinks(isTestRun, publicationFilter),
            musicCataloger.CatalogYearbooksLinks(isTestRun, publicationFilter),
            musicCataloger.CatalogBrochuresAndBookletsLinks(isTestRun, publicationFilter)
        };

        var mediatorTasks = new List<Task> { mediatorCataloger.CatalogMediatorLinks(publicationFilter) };
        var videoTasks = new List<Task> { videoCataloger.CatalogVideoLinks(isTestRun, publicationFilter) };

        await Task.WhenAll([.. bibleTasks, .. musicTasks, .. mediatorTasks, .. videoTasks]);
        logger.Debug("=== CATALOGING PHASE COMPLETED ===\n");
        return 0;
    }

    private static async Task<int> RunSeedDatabaseAsync(
        DbSeeder dbSeeder,
        ILogger logger,
        HashSet<string>? publicationFilter,
        bool isTestRun)
    {
        try
        {
            await dbSeeder.Seed(publicationFilter);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Seeding failed");
            return 1;
        }

        if (!isTestRun)
        {
            return 0;
        }

        try
        {
            logger.Information("=== Starting on-demand fetching test ===");
            await dbSeeder.TestOnDemandFetching();
            logger.Information("=== On-demand fetching test completed ===");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "On-demand fetching test failed");
        }

        return 0;
    }

    private static async Task<int> RunCatalogValidatorAsync(
        ServiceProvider serviceProvider,
        ILogger logger,
        HashSet<string>? publicationFilter)
    {
        using var validateScope = serviceProvider.CreateScope();
        var validateDb = validateScope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var httpClient = serviceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        await CatalogValidator.ValidateAsync(validateDb, httpClient, logger);
        var eSeedValid = await CatalogValidator.ValidateEnglishSeedContentAsync(validateDb, logger, publicationFilter);
        if (!eSeedValid)
        {
            logger.Error("E-seed validation failed: one or more publications have <=0 tracks or (if sectioned) 0 sections. Failing cataloger.");
            return 1;
        }

        return 0;
    }

    private static int FinalizeIndexZip(ILogger logger, long originalIndexFileSize, bool isTestRun)
    {
        SqliteConnection.ClearAllPools();
        logger.Debug("All SQLite connections closed. Safe to zip database.");

        ZipFiles();

        var newIndexFileSize =
            (new FileInfo($"{DirectoryHelper.IndexDirectory}/{AppConstants.FilePaths.MediaIndexZipFileName}")).Length;

        logger.Debug("Old size: {OldSize}kb", originalIndexFileSize / 1024);
        logger.Debug("New size: {NewSize}kb", newIndexFileSize / 1024);

        if (!isTestRun && newIndexFileSize < originalIndexFileSize &&
            (originalIndexFileSize - newIndexFileSize) > (1024 * 1024))
        {
            throw new InvalidOperationException($"New index file size ({newIndexFileSize / 1024}kb) is significantly smaller than old index file size ({originalIndexFileSize / 1024}kb). This could indicate data loss.");
        }

        return 0;
    }



    private static void ZipFiles()
    {
        var zipIndex = $"{DirectoryHelper.IndexDirectory}/{AppConstants.FilePaths.MediaIndexZipFileName}";
        if (File.Exists(zipIndex))
        {
            File.Delete(zipIndex);
        }

        ZipFile.CreateFromDirectory(Path.Combine(DirectoryHelper.IndexDirectory, AppConstants.FilePaths.MediaIndexCatalogOutputDbDirectoryName), zipIndex);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(path))
        {
            DeleteDirectory(directory);
        }

        var files = Directory.GetFiles(path);

        foreach (var file in files)
        {
            if (file.EndsWith(AppConstants.FilePaths.MediaIndexZipFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cannot delete {path}: the file is in use by another process (e.g. a previous cataloger run). Close other instances and try again.",
                    ex);
            }
        }

        if (Directory.GetFiles(path).Length > 0
            || Directory.GetDirectories(path).Length > 0)
        {
            return;
        }

        try
        {
            Directory.Delete(path);
        }
        catch (IOException)
        {
            Directory.Delete(path);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(path);
        }
    }

    /// <summary>
    /// Parses --publications=code1,code2,... or --retry-failed[=path]. Returns null for full run.
    /// </summary>
    private static HashSet<string>? ParsePublicationFilter(string[] args, ILogger logger, string indexDirectory)
    {
        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        var pubArg = args.FirstOrDefault(a => a.StartsWith("--publications=", cmp));
        if (pubArg != null)
        {
            var list = pubArg["--publications=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (list.Length == 0)
            {
                logger.Warning("--publications= was empty, ignoring");
                return null;
            }
            var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            logger.Information("Publication filter: {Count} code(s) from --publications", set.Count);
            return set;
        }

        var retryArg = args.FirstOrDefault(a => a.StartsWith("--retry-failed", cmp));
        if (retryArg != null)
        {
            var path = retryArg.Length > "--retry-failed".Length && retryArg.AsSpan()["--retry-failed".Length] == '='
                ? retryArg["--retry-failed=".Length..].Trim()
                : Path.Combine(indexDirectory, AppConstants.FilePaths.CatalogerLastRunFailedListFileName);
            if (!File.Exists(path))
            {
                logger.Error("Retry file not found: {Path}. Run a full catalog first; failed publications are written there.", path);
                throw new InvalidOperationException($"Retry file not found: {path}");
            }
            var lines = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToList();
            if (lines.Count == 0)
            {
                logger.Warning("Retry file was empty, ignoring");
                return null;
            }
            var set = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
            logger.Information("Publication filter: {Count} code(s) from {Path}", set.Count, path);
            return set;
        }

        return null;
    }

}
