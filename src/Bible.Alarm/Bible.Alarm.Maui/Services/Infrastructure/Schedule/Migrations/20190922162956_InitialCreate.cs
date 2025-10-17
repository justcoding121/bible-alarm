using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Services.Infrastructure.Schedule.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlarmSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    IsEnabled = table.Column<bool>(nullable: false),
                    Hour = table.Column<int>(nullable: false),
                    Minute = table.Column<int>(nullable: false),
                    Second = table.Column<int>(nullable: false),
                    DaysOfWeek = table.Column<int>(nullable: false),
                    MusicEnabled = table.Column<bool>(nullable: false),
                    SnoozeMinutes = table.Column<int>(nullable: false),
                    CurrentPlayItem = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlarmMusic",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MusicType = table.Column<int>(nullable: false),
                    PublicationCode = table.Column<string>(nullable: true),
                    LanguageCode = table.Column<string>(nullable: true),
                    TrackNumber = table.Column<int>(nullable: false),
                    Repeat = table.Column<bool>(nullable: false),
                    AlarmScheduleId = table.Column<int>(nullable: false)
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
                name: "BibleReadingSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageCode = table.Column<string>(nullable: true),
                    PublicationCode = table.Column<string>(nullable: true),
                    BookNumber = table.Column<int>(nullable: false),
                    ChapterNumber = table.Column<int>(nullable: false),
                    AlarmScheduleId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleReadingSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleReadingSchedules_AlarmSchedules_AlarmScheduleId",
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
                name: "IX_BibleReadingSchedules_AlarmScheduleId",
                table: "BibleReadingSchedules",
                column: "AlarmScheduleId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlarmMusic");

            migrationBuilder.DropTable(
                name: "BibleReadingSchedules");

            migrationBuilder.DropTable(
                name: "AlarmSchedules");
        }
    }
}
