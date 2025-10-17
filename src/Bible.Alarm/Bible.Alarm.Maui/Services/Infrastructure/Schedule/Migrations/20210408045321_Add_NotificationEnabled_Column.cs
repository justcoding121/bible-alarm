using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Services.Infrastructure.Schedule.Migrations
{
    public partial class Add_NotificationEnabled_Column : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotificationEnabled",
                table: "AlarmSchedules",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationEnabled",
                table: "AlarmSchedules");
        }
    }
}
