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
                name: "AudioSourceBaseUrls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSourceBaseUrls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseUrlId = table.Column<int>(type: "INTEGER", nullable: false),
                    UrlPath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioSources_AudioSourceBaseUrls_BaseUrlId",
                        column: x => x.BaseUrlId,
                        principalTable: "AudioSourceBaseUrls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayLanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublications_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiblePublications_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MelodyMusics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayLanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MelodyMusics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MelodyMusics_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocalMusics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayLanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocalMusics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocalMusics_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VocalMusics_Languages_LanguageId",
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
                    SourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    MelodyMusicId = table.Column<int>(type: "INTEGER", nullable: true),
                    VocalMusicId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicTracks_AudioSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSources",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicTracks_MelodyMusics_MelodyMusicId",
                        column: x => x.MelodyMusicId,
                        principalTable: "MelodyMusics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicTracks_VocalMusics_VocalMusicId",
                        column: x => x.VocalMusicId,
                        principalTable: "VocalMusics",
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
                    SourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    BiblePublicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    BiblePublicationSectionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublicationTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublicationTracks_AudioSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSources",
                        principalColumn: "Id");
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
                name: "IX_AudioSources_BaseUrlId",
                table: "AudioSources",
                column: "BaseUrlId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublications_Code_LanguageId",
                table: "BiblePublications",
                columns: new[] { "Code", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublications_DisplayLanguageId",
                table: "BiblePublications",
                column: "DisplayLanguageId");

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
                columns: new[] { "BiblePublicationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTracks_BiblePublicationSectionId_Number",
                table: "BiblePublicationTracks",
                columns: new[] { "BiblePublicationSectionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTracks_SourceId",
                table: "BiblePublicationTracks",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MelodyMusics_Code",
                table: "MelodyMusics",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MelodyMusics_DisplayLanguageId",
                table: "MelodyMusics",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_MelodyMusicId",
                table: "MusicTracks",
                column: "MelodyMusicId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_SourceId",
                table: "MusicTracks",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_VocalMusicId",
                table: "MusicTracks",
                column: "VocalMusicId");

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusics_Code_LanguageId",
                table: "VocalMusics",
                columns: new[] { "Code", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusics_DisplayLanguageId",
                table: "VocalMusics",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusics_LanguageId",
                table: "VocalMusics",
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
                name: "AudioSources");

            migrationBuilder.DropTable(
                name: "MelodyMusics");

            migrationBuilder.DropTable(
                name: "VocalMusics");

            migrationBuilder.DropTable(
                name: "BiblePublications");

            migrationBuilder.DropTable(
                name: "AudioSourceBaseUrls");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
