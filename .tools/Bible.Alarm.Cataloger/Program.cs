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
using CatalogValidator = Bible.Alarm.Cataloger.Utility.CatalogValidator;
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
using DirectoryHelper = Bible.Alarm.Cataloger.Utility.DirectoryHelper;
using ILogger = Serilog.ILogger;

namespace Bible.Alarm.Cataloger;

public class Program
{

    // Bible publication codes to catalog (from centralized JwSourceHelper)


    public static async Task<int> Main(string[] args)
    {
        bool verboseLogging = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase) ||
                             args.Contains("-v", StringComparer.OrdinalIgnoreCase);
        bool quietLogging = args.Contains("--quiet", StringComparer.OrdinalIgnoreCase) ||
                           args.Contains("-q", StringComparer.OrdinalIgnoreCase);

        var logLevelEnv = Environment.GetEnvironmentVariable("CATALOGER_LOG_LEVEL");
        if (!string.IsNullOrWhiteSpace(logLevelEnv))
        {
            var level = logLevelEnv.Trim();
            if (level.Equals("Quiet", StringComparison.OrdinalIgnoreCase) || level.Equals("Minimal", StringComparison.OrdinalIgnoreCase))
                quietLogging = true;
            else if (level.Equals("Debug", StringComparison.OrdinalIgnoreCase) || level.Equals("Verbose", StringComparison.OrdinalIgnoreCase))
                verboseLogging = true;
        }

        if (!quietLogging && string.Equals(Environment.GetEnvironmentVariable("CATALOGER_QUIET"), "1", StringComparison.OrdinalIgnoreCase))
            quietLogging = true;

        // Log levels: --verbose/-v or CATALOGER_LOG_LEVEL=Debug → full (Catalogers/Seeders/Utility at Debug). Default → progress (Information). --quiet/-q or CATALOGER_LOG_LEVEL=Quiet → minimal (Catalogers/Seeders/Utility at Warning; only Program phase lines and size).
        var minimumLevel = verboseLogging ? LogEventLevel.Debug : LogEventLevel.Information;
        var catalogerLevel = quietLogging ? LogEventLevel.Warning : LogEventLevel.Information;

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

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddSerilog(Log.Logger);
            // Entity Framework Core logs only show warnings and errors
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
        services.AddSingleton(_ => Log.Logger);

        services.AddDbContext<MediaDbContext>(options =>
        {
            var indexDir = DirectoryHelper.IndexDirectory;
            var dbDir = Path.Combine(new DirectoryInfo(indexDir).FullName, "db");
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            var dbPath = Path.Combine(dbDir, "mediaIndex.db");

            var connectionString = $"Data Source={dbPath};";

            // Specify migrations assembly so EF Core can find and apply all migrations
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
        services.AddSingleton<System.Net.Http.HttpClient>(); // For LanguageContentService
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationService, Bible.Alarm.Shared.Services.Media.BiblePublicationService>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService, Bible.Alarm.Shared.Services.Media.LanguageContentService>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationSectionService, Bible.Alarm.Shared.Services.Media.BiblePublicationSectionService>();
        services.AddTransient<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationTrackService, Bible.Alarm.Shared.Services.Media.BiblePublicationTrackService>();

        await using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger>();

        bool isTestRun = args.Contains("--test-run", StringComparer.OrdinalIgnoreCase) || 
                         args.Contains("--test-mode", StringComparer.OrdinalIgnoreCase) ||
                         args.Contains("--TestRun", StringComparer.OrdinalIgnoreCase);

        HashSet<string>? publicationFilter = ParsePublicationFilter(args, logger, DirectoryHelper.IndexDirectory);
        if (publicationFilter != null)
        {
            logger.Information("=== PUBLICATION FILTER: Processing only {Count} publication(s) ===", publicationFilter.Count);
        }

        if (isTestRun)
        {
            logger.Information("=== TEST RUN MODE: Processing English (E), Malayalam (MY), and Arabic (A) languages per publication ===");
        }

        try
        {
            var indexZipPath = $"{DirectoryHelper.IndexDirectory}/index.zip";
            var indexZipFile = new FileInfo(indexZipPath);
            var originalIndexFileSize = indexZipFile.Exists ? indexZipFile.Length : 0;

            DeleteDirectory(DirectoryHelper.IndexDirectory);

            var bibleTasks = new List<Task>();

            // Use case-insensitive dictionaries for language codes
            // LanguageInfo contains both name and direction (ltr/rtl)
            var languageCodeToInfoMappings = new ConcurrentDictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
            var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            IReadOnlyDictionary<(string LanguageCode, string PublicationCode), string>? localizedPublicationNames = null;

            // Get DbSeeder to use as IDataPersister (same instance will be used for seeding later)
            // Create it directly from serviceProvider to keep it alive
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var downloadUtility = serviceProvider.GetRequiredService<DownloadUtility>();
            var dbSeederLogger = serviceProvider.GetRequiredService<ILogger>();
            var failedListPath = Path.Combine(DirectoryHelper.IndexDirectory, "last_run_failed.txt");
            IDataPersister dataPersister = new DbSeeder(dbSeederLogger, scopeFactory, downloadUtility, isTestRun, failedListPath);

            await using (var catalogerScope = serviceProvider.CreateAsyncScope())
            {
                // Get logger and download utility from DI, but pass dataPersister manually
                var catalogerLogger = catalogerScope.ServiceProvider.GetRequiredService<ILogger>();
                var catalogerDownloadUtility = catalogerScope.ServiceProvider.GetRequiredService<DownloadUtility>();
                
                // Create catalogers with dataPersister
                var bibleCataloger = new BibleCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
                var musicCataloger = new MusicCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
                var mediatorCataloger = new MediatorCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);
                var videoCataloger = new VideoCataloger(catalogerLogger, catalogerDownloadUtility, dataPersister);

                // Create a dictionary with publication codes (names will be extracted from API)
                var biblePublicationCodeToNameMappings = JwSourceHelper.BiblePublicationCodes.ToDictionary(
                    code => code, 
                    code => code); // Temporary name, will be replaced by API response

                // === PHASE 1: DISCOVERY ===
                // Discover all languages for all publications and sections using alllangs=1 and langwritten=E
                logger.Information("=== PHASE 1: DISCOVERY ===");
                await bibleCataloger.DiscoverLanguages(biblePublicationCodeToNameMappings, isTestRun);
                // TODO: Add discovery methods for Music, Drama, Video catalogers
                logger.Information("=== DISCOVERY PHASE COMPLETED ===\n");

                var mediatorLinksValid = await CatalogValidator.ValidateMediatorLinksAsync(catalogerLogger, catalogerDownloadUtility);
                if (!mediatorLinksValid)
                {
                    logger.Error("Mediator link validation failed: one or more category URLs did not return valid category.media. Failing cataloger.");
                    return 1;
                }

                // === PHASE 2: CATALOGING ===
                // Now catalog content for discovered languages
                logger.Information("=== PHASE 2: CATALOGING ===");
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

                var mediatorTasks = new List<Task>
                {
                    mediatorCataloger.CatalogMediatorLinks(isTestRun, publicationFilter)
                };

                var videoTasks = new List<Task>
                {
                    videoCataloger.CatalogVideoLinks(isTestRun, publicationFilter)
                };

                await Task.WhenAll([.. bibleTasks, .. musicTasks, .. mediatorTasks, .. videoTasks]);
                logger.Information("=== CATALOGING PHASE COMPLETED ===\n");

                // Capture localized publication names from Bible cataloger
                localizedPublicationNames = bibleCataloger.LocalizedPublicationNames;
            }

            // Use the same DbSeeder instance that was used as dataPersister
            if (dataPersister is DbSeeder dbSeeder)
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

                // Test on-demand fetching in test mode
                if (isTestRun)
                {
                    try
                    {
                        logger.Information("=== Starting on-demand fetching test ===");
                        await dbSeeder.TestOnDemandFetching();
                        logger.Information("=== On-demand fetching test completed ===");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "On-demand fetching test failed");
                        // Don't fail the entire process, just log the error
                    }
                }
            }
            else
            {
                logger.Error("dataPersister is not a DbSeeder instance. Cannot seed database.");
                return 1;
            }

            using (var validateScope = serviceProvider.CreateScope())
            {
                var validateDb = validateScope.ServiceProvider.GetRequiredService<MediaDbContext>();
                var httpClient = serviceProvider.GetRequiredService<System.Net.Http.HttpClient>();
                await CatalogValidator.ValidateAsync(validateDb, httpClient, logger);
                var eSeedValid = await CatalogValidator.ValidateEnglishSeedContentAsync(validateDb, logger, publicationFilter);
                if (!eSeedValid)
                {
                    logger.Error("E-seed validation failed: one or more publications have <=0 tracks or (if sectioned) 0 sections. Failing cataloger.");
                    return 1;
                }
            }

            SqliteConnection.ClearAllPools();
            logger.Information("All SQLite connections closed. Safe to zip database.");

            ZipFiles();

            var newIndexFileSize =
                (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

            logger.Information("Old size: {OldSize}kb", originalIndexFileSize / 1024);
            logger.Information("New size: {NewSize}kb", newIndexFileSize / 1024);

            // Only check if new size is significantly smaller (could indicate data loss)
            // Size increases are expected when new content is added
            if (!isTestRun && newIndexFileSize < originalIndexFileSize &&
                (originalIndexFileSize - newIndexFileSize) > (1024 * 1024)) // 1 MB threshold
            {
                throw new ApplicationException($"New index file size ({newIndexFileSize / 1024}kb) is significantly smaller than old index file size ({originalIndexFileSize / 1024}kb). This could indicate data loss.");
            }

            // Media index is now packaged with the app only, updated on app updates
            // No longer publishing to AWS S3/CloudFront
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during cataloging");
            return 1;
        }
        finally
        {
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }

            Log.CloseAndFlush();
        }

        var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
        if (!File.Exists(zipIndex))
        {
            logger.Error("Cataloging failed to create zip file.");
            return 1;
        }

        return 0;
    }



    private static void ZipFiles()
    {
        var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
        if (File.Exists(zipIndex))
        {
            File.Delete(zipIndex);
        }

        ZipFile.CreateFromDirectory($"{Path.Combine(DirectoryHelper.IndexDirectory, "db")}", zipIndex);
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
            if (file.EndsWith("index.zip"))
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
        foreach (var arg in args)
        {
            if (arg.StartsWith("--publications=", cmp))
            {
                var list = arg.Substring("--publications=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (list.Length == 0)
                {
                    logger.Warning("--publications= was empty, ignoring");
                    return null;
                }
                var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                logger.Information("Publication filter: {Count} code(s) from --publications", set.Count);
                return set;
            }
        }

        foreach (var arg in args)
        {
            if (!arg.StartsWith("--retry-failed", cmp))
            {
                continue;
            }
            var path = arg.Length > "--retry-failed".Length && arg.AsSpan()["--retry-failed".Length] == '='
                ? arg.Substring("--retry-failed=".Length).Trim()
                : Path.Combine(indexDirectory, "last_run_failed.txt");
            if (!File.Exists(path))
            {
                logger.Error("Retry file not found: {Path}. Run a full catalog first; failed publications are written there.", path);
                throw new InvalidOperationException($"Retry file not found: {path}");
            }
            var lines = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
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
