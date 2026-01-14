using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bible.Alarm.Shared.Database.Migrations.Media
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Direction = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MelodyMusic",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MelodyMusic", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublications_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocalMusic",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocalMusic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocalMusic_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    BiblePublicationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublicationSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublicationSections_BiblePublications_BiblePublicationId",
                        column: x => x.BiblePublicationId,
                        principalTable: "BiblePublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DownloadCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OriginalTrackNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    MelodyMusicId = table.Column<int>(type: "INTEGER", nullable: true),
                    VocalMusicId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicTracks_MelodyMusic_MelodyMusicId",
                        column: x => x.MelodyMusicId,
                        principalTable: "MelodyMusic",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicTracks_VocalMusic_VocalMusicId",
                        column: x => x.VocalMusicId,
                        principalTable: "VocalMusic",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    BiblePublicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    BiblePublicationSectionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublicationTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublicationTracks_BiblePublicationSections_BiblePublicationSectionId",
                        column: x => x.BiblePublicationSectionId,
                        principalTable: "BiblePublicationSections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BiblePublicationTracks_BiblePublications_BiblePublicationId",
                        column: x => x.BiblePublicationId,
                        principalTable: "BiblePublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublications_Code_LanguageId",
                table: "BiblePublications",
                columns: new[] { "Code", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublications_LanguageId",
                table: "BiblePublications",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationSections_BiblePublicationId_Number",
                table: "BiblePublicationSections",
                columns: new[] { "BiblePublicationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTracks_BiblePublicationId_Number",
                table: "BiblePublicationTracks",
                columns: new[] { "BiblePublicationId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTracks_BiblePublicationSectionId_Number",
                table: "BiblePublicationTracks",
                columns: new[] { "BiblePublicationSectionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MelodyMusic_Code",
                table: "MelodyMusic",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_MelodyMusicId",
                table: "MusicTracks",
                column: "MelodyMusicId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_VocalMusicId",
                table: "MusicTracks",
                column: "VocalMusicId");

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusic_Code_LanguageId",
                table: "VocalMusic",
                columns: new[] { "Code", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusic_LanguageId",
                table: "VocalMusic",
                column: "LanguageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiblePublicationTracks");

            migrationBuilder.DropTable(
                name: "MusicTracks");

            migrationBuilder.DropTable(
                name: "BiblePublicationSections");

            migrationBuilder.DropTable(
                name: "MelodyMusic");

            migrationBuilder.DropTable(
                name: "VocalMusic");

            migrationBuilder.DropTable(
                name: "BiblePublications");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
