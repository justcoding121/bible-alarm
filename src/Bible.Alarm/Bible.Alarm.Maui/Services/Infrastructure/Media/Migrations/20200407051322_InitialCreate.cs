using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Services.Infrastructure.Media.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioSource",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(nullable: true),
                    LookUpPath = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSource", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BibleTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Code = table.Column<string>(nullable: true),
                    DisplayLanguageId = table.Column<int>(nullable: false),
                    LanguageId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleTranslations_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BibleTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MelodyMusic",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Code = table.Column<string>(nullable: true),
                    DisplayLanguageId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MelodyMusic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MelodyMusic_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocalMusic",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Code = table.Column<string>(nullable: true),
                    DisplayLanguageId = table.Column<int>(nullable: false),
                    LanguageId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocalMusic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocalMusic_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VocalMusic_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BibleBook",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Number = table.Column<int>(nullable: false),
                    BibleTranslationId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleBook", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleBook_BibleTranslations_BibleTranslationId",
                        column: x => x.BibleTranslationId,
                        principalTable: "BibleTranslations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicTrack",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    SourceId = table.Column<int>(nullable: true),
                    MelodyMusicId = table.Column<int>(nullable: true),
                    VocalMusicId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicTrack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicTrack_MelodyMusic_MelodyMusicId",
                        column: x => x.MelodyMusicId,
                        principalTable: "MelodyMusic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MusicTrack_AudioSource_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MusicTrack_VocalMusic_VocalMusicId",
                        column: x => x.VocalMusicId,
                        principalTable: "VocalMusic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BibleChapter",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(nullable: false),
                    SourceId = table.Column<int>(nullable: true),
                    BibleBookId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleChapter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleChapter_BibleBook_BibleBookId",
                        column: x => x.BibleBookId,
                        principalTable: "BibleBook",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BibleChapter_AudioSource_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BibleBook_BibleTranslationId",
                table: "BibleBook",
                column: "BibleTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_BibleChapter_BibleBookId",
                table: "BibleChapter",
                column: "BibleBookId");

            migrationBuilder.CreateIndex(
                name: "IX_BibleChapter_SourceId",
                table: "BibleChapter",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BibleTranslations_DisplayLanguageId",
                table: "BibleTranslations",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_BibleTranslations_LanguageId",
                table: "BibleTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_MelodyMusic_DisplayLanguageId",
                table: "MelodyMusic",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTrack_MelodyMusicId",
                table: "MusicTrack",
                column: "MelodyMusicId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTrack_SourceId",
                table: "MusicTrack",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTrack_VocalMusicId",
                table: "MusicTrack",
                column: "VocalMusicId");

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusic_DisplayLanguageId",
                table: "VocalMusic",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusic_LanguageId",
                table: "VocalMusic",
                column: "LanguageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BibleChapter");

            migrationBuilder.DropTable(
                name: "MusicTrack");

            migrationBuilder.DropTable(
                name: "BibleBook");

            migrationBuilder.DropTable(
                name: "MelodyMusic");

            migrationBuilder.DropTable(
                name: "AudioSource");

            migrationBuilder.DropTable(
                name: "VocalMusic");

            migrationBuilder.DropTable(
                name: "BibleTranslations");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
