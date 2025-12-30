#nullable enable

using System;
using System.IO;
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
        Console.WriteLine("  dotnet run -- status [Schedule|Media]    - Show migration status");
        Console.WriteLine("  dotnet run -- generate-empty-schedule-db [outputPath] - Generate empty Schedule database with all migrations applied");
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
            await ShowMigrationStatusForContext<ScheduleDbContext>();
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
        var walPath = outputPath + "-wal";
        var shmPath = outputPath + "-shm";
        if (File.Exists(walPath))
        {
            try { File.Delete(walPath); } catch { }
        }
        if (File.Exists(shmPath))
        {
            try { File.Delete(shmPath); } catch { }
        }

        // Create temporary database with all migrations applied
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}_{AppConstants.Database.ScheduleDatabaseFileName}");
        try
        {
            var tempConnectionString = string.Format(
                AppConstants.Database.ScheduleDatabaseConnectionStringFormat,
                tempDbPath);

            var tempOptionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
            tempOptionsBuilder.UseSqlite(tempConnectionString, b => b.MigrationsAssembly("Bible.Alarm.Shared"));

            using (var tempContext = new ScheduleDbContext(tempOptionsBuilder.Options))
            {
                // Apply all migrations to create the database with latest schema
                Console.WriteLine("Applying all migrations...");
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
}

