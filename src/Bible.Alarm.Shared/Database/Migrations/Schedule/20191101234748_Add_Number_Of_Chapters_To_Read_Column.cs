using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Shared.Database.Migrations.Schedule;

public partial class AddNumberOfTracksToReadColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "NumberOfTracksToRead",
            table: "AlarmSchedules",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "NumberOfTracksToRead",
            table: "AlarmSchedules");
    }
}
