using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace Bible.Alarm.Services.Infrastructure.Schedule.Migrations
{
    public partial class Add_FinishedDuration_Column : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "FinishedDuration",
                table: "BibleReadingSchedules",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "AlwaysPlayFromStart",
                table: "AlarmSchedules",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinishedDuration",
                table: "BibleReadingSchedules");

            migrationBuilder.DropColumn(
                name: "AlwaysPlayFromStart",
                table: "AlarmSchedules");
        }
    }
}
