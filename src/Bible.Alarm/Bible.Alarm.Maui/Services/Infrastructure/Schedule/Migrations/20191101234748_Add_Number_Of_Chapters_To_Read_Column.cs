using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Services.Infrastructure.Schedule.Migrations
{
    public partial class Add_Number_Of_Chapters_To_Read_Column : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfChaptersToRead",
                table: "AlarmSchedules",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfChaptersToRead",
                table: "AlarmSchedules");
        }
    }
}
