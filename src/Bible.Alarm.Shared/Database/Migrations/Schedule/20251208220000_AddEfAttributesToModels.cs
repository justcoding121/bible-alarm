using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Shared.Database.Migrations.Schedule;

/// <inheritdoc />
public partial class AddEfAttributesToModels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add indexes for AlarmSchedule
        migrationBuilder.CreateIndex(
            name: "IX_AlarmSchedules_IsEnabled",
            table: "AlarmSchedules",
            column: "IsEnabled");

        migrationBuilder.CreateIndex(
            name: "IX_AlarmSchedules_Hour_Minute",
            table: "AlarmSchedules",
            columns: new[] { "Hour", "Minute" });

        // Add indexes for AlarmMusic
        migrationBuilder.CreateIndex(
            name: "IX_AlarmMusic_PublicationCode_LanguageCode",
            table: "AlarmMusic",
            columns: new[] { "PublicationCode", "LanguageCode" });

        // Add indexes for AlarmNotification
        migrationBuilder.CreateIndex(
            name: "IX_AlarmNotifications_ScheduledTime",
            table: "AlarmNotifications",
            column: "ScheduledTime");

        migrationBuilder.CreateIndex(
            name: "IX_AlarmNotifications_Sent_Fired",
            table: "AlarmNotifications",
            columns: new[] { "Sent", "Fired" });

        // Add indexes for BibleReadingSchedule
        migrationBuilder.CreateIndex(
            name: "IX_BibleReadingSchedules_PublicationCode_LanguageCode",
            table: "BibleReadingSchedules",
            columns: new[] { "PublicationCode", "LanguageCode" });

        // GeneralSettings already has index on Key from previous migration
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_BibleReadingSchedules_PublicationCode_LanguageCode",
            table: "BibleReadingSchedules");

        migrationBuilder.DropIndex(
            name: "IX_AlarmNotifications_Sent_Fired",
            table: "AlarmNotifications");

        migrationBuilder.DropIndex(
            name: "IX_AlarmNotifications_ScheduledTime",
            table: "AlarmNotifications");

        migrationBuilder.DropIndex(
            name: "IX_AlarmMusic_PublicationCode_LanguageCode",
            table: "AlarmMusic");

        migrationBuilder.DropIndex(
            name: "IX_AlarmSchedules_Hour_Minute",
            table: "AlarmSchedules");

        migrationBuilder.DropIndex(
            name: "IX_AlarmSchedules_IsEnabled",
            table: "AlarmSchedules");
    }
}

