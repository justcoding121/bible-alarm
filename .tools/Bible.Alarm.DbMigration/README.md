# Bible.Alarm Database Migration Tool

This project provides Entity Framework Core migration support for the Bible.Alarm application databases.

## Purpose

This migration project allows you to:
- Create new migrations for `ScheduleDbContext` and `MediaDbContext`
- Apply migrations to databases
- Check migration status
- Manage database schema changes independently from the main application

## Prerequisites

- .NET 10.0 SDK
- Entity Framework Core tools (included via package reference)

## Usage

### Creating Migrations

To create a new migration for the Schedule database:

```bash
dotnet ef migrations add <MigrationName> --project .tools/Bible.Alarm.DbMigration --context ScheduleDbContext --output-dir ../src/Bible.Alarm.Shared/Database/Migrations/Schedule
```

To create a new migration for the Media database:

```bash
dotnet ef migrations add <MigrationName> --project .tools/Bible.Alarm.DbMigration --context MediaDbContext --output-dir ../src/Bible.Alarm.Shared/Database/Migrations/Media
```

### Applying Migrations

To apply pending migrations to the Schedule database:

```bash
dotnet ef database update --project .tools/Bible.Alarm.DbMigration --context ScheduleDbContext
```

To apply pending migrations to the Media database:

```bash
dotnet ef database update --project .tools/Bible.Alarm.DbMigration --context MediaDbContext
```

### Checking Migration Status

To list all migrations:

```bash
dotnet run --project .tools/Bible.Alarm.DbMigration -- list
```

To check migration status for a specific context:

```bash
dotnet run --project .tools/Bible.Alarm.DbMigration -- list Schedule
dotnet run --project .tools/Bible.Alarm.DbMigration -- list Media
```

To show migration status:

```bash
dotnet run --project .tools/Bible.Alarm.DbMigration -- status
```

## Project Structure

- `ScheduleDbContextFactory.cs` - Design-time factory for ScheduleDbContext
- `MediaDbContextFactory.cs` - Design-time factory for MediaDbContext
- `Program.cs` - Console application for migration management

## Design-Time Factories

The design-time factories (`ScheduleDbContextFactory` and `MediaDbContextFactory`) are used by Entity Framework Core tools to create instances of the DbContext classes during migration operations. They configure the database connection using temporary database files in the current directory.

## Notes

- Migrations are stored in `src/Bible.Alarm.Shared/Database/Migrations/` with separate folders for Schedule and Media migrations
- The migration project references the `Bible.Alarm.Shared` project to access the DbContext classes and models
- Database files created during design-time operations are temporary and can be safely deleted

