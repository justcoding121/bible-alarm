#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
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
        using var context = factory.CreateDbContext(Array.Empty<string>());

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
        using var context = factory.CreateDbContext(Array.Empty<string>());

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
            return (IDesignTimeDbContextFactory<TContext>)(object)new ScheduleDbContextFactory();
        }
        else if (typeof(TContext) == typeof(MediaDbContext))
        {
            return (IDesignTimeDbContextFactory<TContext>)(object)new MediaDbContextFactory();
        }

        throw new NotSupportedException($"No factory found for context type {typeof(TContext).Name}");
    }
}

