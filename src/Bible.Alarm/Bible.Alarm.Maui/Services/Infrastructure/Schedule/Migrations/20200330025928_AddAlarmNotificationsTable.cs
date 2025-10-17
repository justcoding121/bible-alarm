using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace Bible.Alarm.Services.Infrastructure.Schedule.Migrations
{
    public partial class AddAlarmNotificationsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LatestAlarmNotificationId",
                table: "AlarmSchedules",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AlarmNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledTime = table.Column<DateTimeOffset>(nullable: false),
                    Sent = table.Column<bool>(nullable: false),
                    Fired = table.Column<bool>(nullable: false),
                    AlarmScheduleId = table.Column<int>(nullable: false),
                    CancellationRequested = table.Column<bool>(nullable: false),
                    Cancelled = table.Column<bool>(nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_AlarmScheduleId",
                table: "AlarmNotifications",
                column: "AlarmScheduleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlarmNotifications");

            migrationBuilder.DropColumn(
                name: "LatestAlarmNotificationId",
                table: "AlarmSchedules");
        }
    }
}
