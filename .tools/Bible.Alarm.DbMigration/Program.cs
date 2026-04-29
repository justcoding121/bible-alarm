#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bible.Alarm.DbMigration;

/// <summary>
/// Console application for managing Entity Framework Core migrations.
/// This tool allows you to create and apply migrations for ScheduleDbContext and MediaDbContext.
/// </summary>
class Program
{
    private static readonly string[] GenerateEmptyScheduleDbArgs = ["generate-empty-schedule-db"];

    private static void TryDeleteFileBestEffort(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Warning: could not delete file (may be locked) {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Warning: could not delete file {path}: {ex.Message}");
        }
    }

    private static void TryDeleteDirectoryBestEffort(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Warning: could not delete temp directory {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Warning: could not delete temp directory {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Maximum uncompressed bytes per zip entry (mitigates zip bombs; media index files are far smaller).
    /// </summary>
    private const long MaxZipEntryUncompressedBytes = 512L * 1024 * 1024;

    /// <summary>
    /// Maximum number of entries processed (defense in depth for malicious archives).
    /// </summary>
    private const int MaxZipEntryCount = 100_000;

    /// <summary>
    /// Extracts a zip without following absolute paths or parent traversals outside the destination (S5042 / zip slip).
    /// Does not use <see cref="ZipArchiveEntry.ExtractToFile"/> so static analysis accepts the controlled extraction path.
    /// </summary>
    static void ExtractMediaIndexZipSafely(string zipPath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destRoot = Path.GetFullPath(destinationDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!destRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            destRoot += Path.DirectorySeparatorChar;
        }

        using var zipStream = new FileStream(
            zipPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        var processed = 0;
        foreach (var entry in archive.Entries)
        {
            if (++processed > MaxZipEntryCount)
            {
                throw new InvalidDataException($"Zip archive exceeds maximum entry count ({MaxZipEntryCount}).");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var entryRelative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.GetFullPath(Path.Combine(destRoot, entryRelative));
            if (!destinationPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Warning: skipping unsafe zip path: {entry.FullName}");
                continue;
            }

            var entryDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            ExtractZipEntryToFileSafely(entry, destinationPath);
        }
    }

    /// <summary>
    /// Writes a single archive entry to disk after path checks, with a byte cap (S5042).
    /// </summary>
    static void ExtractZipEntryToFileSafely(ZipArchiveEntry entry, string destinationPath)
    {
        using var input = entry.Open();
        using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[65536];
        long written = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (written + read > MaxZipEntryUncompressedBytes)
            {
                throw new InvalidDataException(
                    $"Zip entry '{entry.FullName}' exceeds maximum allowed uncompressed size ({MaxZipEntryUncompressedBytes} bytes).");
            }

            output.Write(buffer, 0, read);
            written += read;
        }
    }

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();
        var contextName = args.Length > 1 ? args[1] : null;

        try
        {
            switch (command)
            {
                case "list":
                    await ListMigrations(contextName);
                    break;
                case "status":
                    await ShowMigrationStatus(contextName);
                    break;
                case "generate-empty-schedule-db":
                    await GenerateEmptyScheduleDatabase(args);
                    break;
                case "apply-and-update-resources":
                    await ApplyMigrationsAndUpdateResources(contextName);
                    break;
                case "list-bible-languages":
                    await ListBibleLanguages(args.Length > 1 ? args[1] : null);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Bible.Alarm Database Migration Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- list [Schedule|Media]     - List all migrations");
        Console.WriteLine("  dotnet run -- status [Schedule|Media]    - Show migration status (checks Resources database for Schedule)");
        Console.WriteLine("  dotnet run -- generate-empty-schedule-db [outputPath] - Generate empty Schedule database with all migrations applied");
        Console.WriteLine("  dotnet run -- apply-and-update-resources [Schedule|Media] - Apply migrations and update Resources database");
        Console.WriteLine($"  dotnet run -- list-bible-languages [path] - List Bible category language names from media index (path: {AppConstants.FilePaths.MediaIndexZipFileName} or {AppConstants.Database.MediaIndexDatabaseFileName}, default: .tools/_index)");
        Console.WriteLine();
        Console.WriteLine("To create migrations, use EF Core tools:");
        Console.WriteLine("  dotnet ef migrations add <MigrationName> --project .tools/Bible.Alarm.DbMigration --context ScheduleDbContext");
        Console.WriteLine("  dotnet ef migrations add <MigrationName> --project .tools/Bible.Alarm.DbMigration --context MediaDbContext");
        Console.WriteLine();
        Console.WriteLine("To apply migrations:");
        Console.WriteLine("  dotnet ef database update --project .tools/Bible.Alarm.DbMigration --context ScheduleDbContext");
        Console.WriteLine("  dotnet ef database update --project .tools/Bible.Alarm.DbMigration --context MediaDbContext");
    }

    static async Task ListMigrations(string? contextName)
    {
        if (string.IsNullOrEmpty(contextName) || contextName.Equals("Schedule", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Schedule Database Migrations:");
            await ListMigrationsForContext<ScheduleDbContext>();
        }

        if (string.IsNullOrEmpty(contextName) || contextName.Equals("Media", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\nMedia Database Migrations:");
            await ListMigrationsForContext<MediaDbContext>();
        }
    }

    static async Task ShowMigrationStatus(string? contextName)
    {
        if (string.IsNullOrEmpty(contextName) || contextName.Equals("Schedule", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Schedule Database Migration Status:");
            await ShowMigrationStatusForScheduleContext();
        }

        if (string.IsNullOrEmpty(contextName) || contextName.Equals("Media", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\nMedia Database Migration Status:");
            await ShowMigrationStatusForContext<MediaDbContext>();
        }
    }

    static async Task ListMigrationsForContext<TContext>() where TContext : DbContext
    {
        var factory = GetFactory<TContext>();
        using var context = factory.CreateDbContext([]);

        var migrations = await context.Database.GetPendingMigrationsAsync();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

        Console.WriteLine($"  Applied migrations: {appliedMigrations.Count()}");
        foreach (var migration in appliedMigrations)
        {
            Console.WriteLine($"    ✓ {migration}");
        }

        Console.WriteLine($"  Pending migrations: {migrations.Count()}");
        foreach (var migration in migrations)
        {
            Console.WriteLine($"    ○ {migration}");
        }
    }

    static async Task ShowMigrationStatusForScheduleContext()
    {
        // Check the Resources database (the bundled database that ships with the app)
        var resourcesDbPath = GetResourcesDatabasePath();
        Console.WriteLine($"  Checking Resources database: {resourcesDbPath}");
        
        if (File.Exists(resourcesDbPath))
        {
            var resourcesConnectionString = string.Format(
                AppConstants.Database.ScheduleDatabaseConnectionStringFormat,
                resourcesDbPath);
            var resourcesOptionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
            resourcesOptionsBuilder.UseSqlite(resourcesConnectionString, b => b.MigrationsAssembly("Bible.Alarm.Shared"));
            
            using (var resourcesContext = new ScheduleDbContext(resourcesOptionsBuilder.Options))
            {
                var pendingMigrations = await resourcesContext.Database.GetPendingMigrationsAsync();
                var appliedMigrations = await resourcesContext.Database.GetAppliedMigrationsAsync();

                Console.WriteLine($"  Applied migrations: {appliedMigrations.Count()}");
                foreach (var migration in appliedMigrations)
                {
                    Console.WriteLine($"    ✓ {migration}");
                }

                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"  ⚠ Resources database is not up to date. {pendingMigrations.Count()} pending migration(s):");
                    foreach (var migration in pendingMigrations)
                    {
                        Console.WriteLine($"    ○ {migration}");
                    }
                    Console.WriteLine($"  Run 'dotnet run -- apply-and-update-resources Schedule' to apply migrations and update Resources database.");
                }
                else
                {
                    Console.WriteLine($"  ✓ Resources database is up to date. {appliedMigrations.Count()} migration(s) applied.");
                }
            }
        }
        else
        {
            Console.WriteLine($"  ⚠ Resources database not found at: {resourcesDbPath}");
            Console.WriteLine($"  Run 'dotnet run -- generate-empty-schedule-db' to create it.");
        }
    }

    static async Task ShowMigrationStatusForContext<TContext>() where TContext : DbContext
    {
        var factory = GetFactory<TContext>();
        using var context = factory.CreateDbContext([]);

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

        if (pendingMigrations.Any())
        {
            Console.WriteLine($"  ⚠ Database is not up to date. {pendingMigrations.Count()} pending migration(s).");
        }
        else
        {
            Console.WriteLine($"  ✓ Database is up to date. {appliedMigrations.Count()} migration(s) applied.");
        }
    }

    static IDesignTimeDbContextFactory<TContext> GetFactory<TContext>() where TContext : DbContext
    {
        if (typeof(TContext) == typeof(ScheduleDbContext))
        {
            return (IDesignTimeDbContextFactory<TContext>)new ScheduleDbContextFactory();
        }

        if (typeof(TContext) == typeof(MediaDbContext))
        {
            return (IDesignTimeDbContextFactory<TContext>)new MediaDbContextFactory();
        }

        throw new NotSupportedException($"No factory found for context type {typeof(TContext).Name}");
    }

    static async Task GenerateEmptyScheduleDatabase(string[] args)
    {
        // Default output path: Resources folder in the main project
        // Resolve to absolute path to avoid issues with relative paths
        var outputPath = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.GetFullPath(Path.Combine("..", "..", "src", "Bible.Alarm", "Resources", AppConstants.Database.ScheduleDatabaseFileName));

        Console.WriteLine($"Generating empty Schedule database with all migrations applied...");
        Console.WriteLine($"Output path: {outputPath}");

        // Ensure output directory exists
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            Console.WriteLine($"Created output directory: {outputDirectory}");
        }

        // Delete existing database if it exists (and any WAL/SHM files)
        if (File.Exists(outputPath))
        {
            try
            {
                File.Delete(outputPath);
                Console.WriteLine("Deleted existing database file");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: Could not delete existing database file (may be locked): {ex.Message}");
                Console.WriteLine("Please close any applications using the database and try again.");
                return;
            }
        }

        // Also clean up any WAL/SHM files
        var walPath = outputPath + AppConstants.Database.SqliteWalFileSuffix;
        var shmPath = outputPath + AppConstants.Database.SqliteShmFileSuffix;
        TryDeleteFileBestEffort(walPath);
        TryDeleteFileBestEffort(shmPath);

        // Create temporary database with all migrations applied
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}_{AppConstants.Database.ScheduleDatabaseFileName}");
        try
        {
            var tempConnectionString = string.Format(
                AppConstants.Database.ScheduleDatabaseConnectionStringFormat,
                tempDbPath);

            var tempOptionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
            tempOptionsBuilder.UseSqlite(tempConnectionString, b => b.MigrationsAssembly("Bible.Alarm.Shared"));
            tempOptionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            using (var tempContext = new ScheduleDbContext(tempOptionsBuilder.Options))
            {
                // Apply all migrations to create the database with latest schema
                Console.WriteLine("Applying all migrations...");

                // Get all available migrations
                var migrationsAssembly = tempContext.Database.GetMigrations();
                Console.WriteLine($"Found {migrationsAssembly.Count()} migrations in assembly");

                await tempContext.Database.MigrateAsync();

                // Verify all migrations are applied
                var appliedMigrations = await tempContext.Database.GetAppliedMigrationsAsync();
                var pendingMigrations = await tempContext.Database.GetPendingMigrationsAsync();

                Console.WriteLine($"Applied migrations: {appliedMigrations.Count()}");
                foreach (var migration in appliedMigrations)
                {
                    Console.WriteLine($"  ✓ {migration}");
                }

                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"Error: {pendingMigrations.Count()} pending migrations found after applying migrations!");
                    foreach (var migration in pendingMigrations)
                    {
                        Console.WriteLine($"  ○ {migration}");
                    }
                    throw new InvalidOperationException("Not all migrations were applied successfully");
                }
                else
                {
                    Console.WriteLine("✓ All migrations applied successfully");
                }

                // Checkpoint WAL to ensure all changes are written to the main database file
                // This is critical when copying the database file, as WAL mode writes changes
                // to a separate WAL file that needs to be checkpointed into the main file
                Console.WriteLine("Checkpointing WAL...");
                var connection = tempContext.Database.GetDbConnection();
                await connection.OpenAsync();
                try
                {
                    using var checkpointCommand = connection.CreateCommand();
                    checkpointCommand.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                    await checkpointCommand.ExecuteNonQueryAsync();
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }

            // Copy the empty database (with schema but no data) to output location
            // After checkpointing, all changes should be in the main database file
            File.Copy(tempDbPath, outputPath, overwrite: true);
            Console.WriteLine($"✓ Successfully generated empty Schedule database at: {outputPath}");
            Console.WriteLine($"  File size: {new FileInfo(outputPath).Length} bytes");

            // Verify the copied database has migrations applied
            var verifyConnectionString = string.Format(
                AppConstants.Database.ScheduleDatabaseConnectionStringFormat,
                outputPath);
            var verifyOptionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
            verifyOptionsBuilder.UseSqlite(verifyConnectionString, b => b.MigrationsAssembly("Bible.Alarm.Shared"));
            using (var verifyContext = new ScheduleDbContext(verifyOptionsBuilder.Options))
            {
                var verifyApplied = await verifyContext.Database.GetAppliedMigrationsAsync();
                var verifyPending = await verifyContext.Database.GetPendingMigrationsAsync();
                Console.WriteLine($"Verification: {verifyApplied.Count()} applied, {verifyPending.Count()} pending");
                if (verifyPending.Any())
                {
                    Console.WriteLine("Warning: Copied database has pending migrations!");
                }
            }
        }
        finally
        {
            // Clean up temporary database
            if (File.Exists(tempDbPath))
            {
                try
                {
                    File.Delete(tempDbPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    static string GetResourcesDatabasePath()
    {
        return Path.GetFullPath(Path.Combine("..", "..", "src", "Bible.Alarm", "Resources", AppConstants.Database.ScheduleDatabaseFileName));
    }

    static async Task ApplyMigrationsAndUpdateResources(string? contextName)
    {
        if (string.IsNullOrEmpty(contextName) || contextName.Equals("Schedule", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Applying migrations and updating Resources database for Schedule...");
            await GenerateEmptyScheduleDatabase(GenerateEmptyScheduleDbArgs);
        }
        else if (contextName.Equals("Media", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Media database is managed separately (shipped as {AppConstants.FilePaths.MediaIndexZipFileName}).");
            Console.WriteLine("Use the cataloger tool to regenerate the Media index database.");
        }
        else
        {
            Console.WriteLine($"Unknown context: {contextName}");
            Console.WriteLine("Supported contexts: Schedule, Media");
        }
    }

    static async Task ListBibleLanguages(string? path)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        var searchDir = string.IsNullOrEmpty(path) ? Path.Combine(repoRoot, ".tools", "_index") : Path.GetFullPath(path);
        string dbPath;
        if (File.Exists(searchDir) && searchDir.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"bible_alarm_media_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                ExtractMediaIndexZipSafely(searchDir, tempDir);
                dbPath = Path.Combine(tempDir, AppConstants.Database.MediaIndexDatabaseFileName);
                if (!File.Exists(dbPath))
                {
                    Console.WriteLine($"Error: {AppConstants.FilePaths.MediaIndexZipFileName} does not contain {AppConstants.Database.MediaIndexDatabaseFileName}");
                    return;
                }
                await QueryBibleLanguages(dbPath);
            }
            finally
            {
                TryDeleteDirectoryBestEffort(tempDir);
            }
            return;
        }
        if (Directory.Exists(searchDir))
        {
            dbPath = Path.Combine(searchDir, AppConstants.Database.MediaIndexDatabaseFileName);
            if (!File.Exists(dbPath))
            {
                var zipPath = Path.Combine(searchDir, AppConstants.FilePaths.MediaIndexZipFileName);
                if (File.Exists(zipPath))
                {
                    path = zipPath;
                    goto extractZip;
                }
                Console.WriteLine($"Error: {AppConstants.Database.MediaIndexDatabaseFileName} not found in {searchDir}");
                return;
            }
        }
        else if (File.Exists(searchDir) && searchDir.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            dbPath = searchDir;
        }
        else
        {
            Console.WriteLine($"Error: path not found or not a media index: {searchDir}");
            return;
        }
        await QueryBibleLanguages(dbPath);
        return;
extractZip:
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"bible_alarm_media_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                ExtractMediaIndexZipSafely(path!, tempDir);
                dbPath = Path.Combine(tempDir, AppConstants.Database.MediaIndexDatabaseFileName);
                if (!File.Exists(dbPath))
                {
                    Console.WriteLine($"Error: {AppConstants.FilePaths.MediaIndexZipFileName} does not contain {AppConstants.Database.MediaIndexDatabaseFileName}");
                    return;
                }
                await QueryBibleLanguages(dbPath);
            }
            finally
            {
                TryDeleteDirectoryBestEffort(tempDir);
            }
        }
    }

    static async Task QueryBibleLanguages(string dbPath)
    {
        var connectionString = string.Format(
            AppConstants.Database.MediaIndexDatabaseConnectionStringFormat,
            dbPath);
        var optionsBuilder = new DbContextOptionsBuilder<MediaDbContext>();
        optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("Bible.Alarm.Shared"));

        using var context = new MediaDbContext(optionsBuilder.Options);
        var languages = await context.PublicationLanguages
            .AsNoTracking()
            .Include(x => x.Language)
            .Include(x => x.Category)
            .Where(x => x.Category != null && x.Category.CategoryCode == "Bible" && x.LanguageId != null && x.Language != null)
            .Select(x => x.Language!.LanguageCode)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        Console.WriteLine($"Bible category languages ({languages.Count}):");
        foreach (var name in languages)
        {
            Console.WriteLine($"  {name}");
        }
    }
}

