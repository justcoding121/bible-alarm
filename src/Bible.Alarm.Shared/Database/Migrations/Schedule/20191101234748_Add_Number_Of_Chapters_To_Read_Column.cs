using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Shared.Database.Migrations.Schedule;

public partial class AddNumberOfChaptersToReadColumn : Migration
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
