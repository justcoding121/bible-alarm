using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bible.Alarm.Shared.Database.Migrations.Schedule
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlarmSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Hour = table.Column<int>(type: "INTEGER", nullable: false),
                    Minute = table.Column<int>(type: "INTEGER", nullable: false),
                    Second = table.Column<int>(type: "INTEGER", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MusicEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SnoozeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NumberOfTracksToPlay = table.Column<int>(type: "INTEGER", nullable: false),
                    AlwaysPlayFromStart = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentPlayItem = table.Column<int>(type: "INTEGER", nullable: false),
                    LatestAlarmNotificationId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneralSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlarmMusic",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PublicationCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SectionCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TrackNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Repeat = table.Column<bool>(type: "INTEGER", nullable: false),
                    AlarmScheduleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmMusic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlarmMusic_AlarmSchedules_AlarmScheduleId",
                        column: x => x.AlarmScheduleId,
                        principalTable: "AlarmSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlarmNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Sent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Fired = table.Column<bool>(type: "INTEGER", nullable: false),
                    AlarmScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CancellationRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cancelled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlarmNotifications_AlarmSchedules_AlarmScheduleId",
                        column: x => x.AlarmScheduleId,
                        principalTable: "AlarmSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PublicationCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SectionCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TrackNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    FinishedDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    AlarmScheduleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublicationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublicationSchedules_AlarmSchedules_AlarmScheduleId",
                        column: x => x.AlarmScheduleId,
                        principalTable: "AlarmSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmMusic_AlarmScheduleId",
                table: "AlarmMusic",
                column: "AlarmScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlarmMusic_PublicationCode_LanguageCode",
                table: "AlarmMusic",
                columns: new[] { "PublicationCode", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_AlarmScheduleId",
                table: "AlarmNotifications",
                column: "AlarmScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_ScheduledTime",
                table: "AlarmNotifications",
                column: "ScheduledTime");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_Sent_Fired",
                table: "AlarmNotifications",
                columns: new[] { "Sent", "Fired" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmSchedules_Hour_Minute",
                table: "AlarmSchedules",
                columns: new[] { "Hour", "Minute" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmSchedules_IsEnabled",
                table: "AlarmSchedules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationSchedules_AlarmScheduleId",
                table: "BiblePublicationSchedules",
                column: "AlarmScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationSchedules_PublicationCode_LanguageCode",
                table: "BiblePublicationSchedules",
                columns: new[] { "PublicationCode", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralSettings_Key",
                table: "GeneralSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlarmMusic");

            migrationBuilder.DropTable(
                name: "AlarmNotifications");

            migrationBuilder.DropTable(
                name: "BiblePublicationSchedules");

            migrationBuilder.DropTable(
                name: "GeneralSettings");

            migrationBuilder.DropTable(
                name: "AlarmSchedules");
        }
    }
}
