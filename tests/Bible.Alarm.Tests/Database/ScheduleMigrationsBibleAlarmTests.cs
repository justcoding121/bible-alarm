#nullable enable

using System.Reflection;
using Bible.Alarm.Shared.Database.Migrations.Schedule;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMigrationsBibleAlarmTests
{
    [Fact]
    public void AddCategoryCodeToAlarmSchedule_Up_and_Down_emit_expected_operations()
    {
        var migration = new AddCategoryCodeToAlarmSchedule();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");

        InvokeMigration(migration, "Up", builder);
        InvokeMigration(migration, "Down", builder);

        Assert.Contains(builder.Operations, op => op.GetType().Name.Contains("AddColumn", StringComparison.Ordinal));
        Assert.Contains(builder.Operations, op => op.GetType().Name.Contains("DropColumn", StringComparison.Ordinal));
    }

    [Fact]
    public void AddLastPlayedAtUtcToAlarmSchedule_Up_and_Down_emit_expected_operations()
    {
        var migration = new AddLastPlayedAtUtcToAlarmSchedule();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");

        InvokeMigration(migration, "Up", builder);
        InvokeMigration(migration, "Down", builder);

        Assert.Contains(builder.Operations, op => op.GetType().Name.Contains("AddColumn", StringComparison.Ordinal));
        Assert.Contains(builder.Operations, op => op.GetType().Name.Contains("DropColumn", StringComparison.Ordinal));
    }

    private static void InvokeMigration(object migration, string methodName, MigrationBuilder builder)
    {
        var method = migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(migration, [builder]);
    }
}
