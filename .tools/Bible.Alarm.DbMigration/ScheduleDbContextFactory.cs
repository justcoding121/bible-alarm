using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace Bible.Alarm.DbMigration;

/// <summary>
/// Design-time factory for ScheduleDbContext.
/// This factory is used by Entity Framework Core tools (e.g., migrations) to create instances of ScheduleDbContext.
/// </summary>
public class ScheduleDbContextFactory : IDesignTimeDbContextFactory<ScheduleDbContext>
{
    public ScheduleDbContext CreateDbContext(string[] args)
    {
        // For design-time (migrations), use a temporary database path
        var dbPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            AppConstants.Database.ScheduleDatabaseFileName);
        
        var connectionString = string.Format(
            AppConstants.Database.ScheduleDatabaseConnectionStringFormat,
            dbPath);
        
        var optionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        
        return new ScheduleDbContext(optionsBuilder.Options);
    }
}

