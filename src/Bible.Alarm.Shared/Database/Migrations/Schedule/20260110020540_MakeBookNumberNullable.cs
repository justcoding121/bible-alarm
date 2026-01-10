using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bible.Alarm.Shared.Database.Migrations.Schedule
{
    /// <inheritdoc />
    public partial class MakeSectionNumberNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SectionNumber",
                table: "BibleReadingSchedules",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SectionNumber",
                table: "BibleReadingSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
