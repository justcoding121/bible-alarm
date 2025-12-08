using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace Bible.Alarm.DbMigration;

/// <summary>
/// Design-time factory for MediaDbContext.
/// This factory is used by Entity Framework Core tools (e.g., migrations) to create instances of MediaDbContext.
/// </summary>
public class MediaDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        // For design-time (migrations), use a temporary database path
        var dbPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            AppConstants.Database.MediaIndexDatabaseFileName);
        
        var connectionString = string.Format(
            AppConstants.Database.MediaIndexDatabaseConnectionStringFormat,
            dbPath);
        
        var optionsBuilder = new DbContextOptionsBuilder<MediaDbContext>();
            optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("Bible.Alarm.Shared"));
        
        return new MediaDbContext(optionsBuilder.Options);
    }
}

