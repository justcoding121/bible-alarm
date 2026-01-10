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
                name: "AudioSourceBaseUrl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSourceBaseUrl", x => x.Id);
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
                name: "AudioSource",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseUrlId = table.Column<int>(type: "INTEGER", nullable: false),
                    UrlPath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioSource_AudioSourceBaseUrl_BaseUrlId",
                        column: x => x.BaseUrlId,
                        principalTable: "AudioSourceBaseUrl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublication",
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
                    table.PrimaryKey("PK_BiblePublication", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublication_Languages_DisplayLanguageId",
                        column: x => x.DisplayLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiblePublication_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MelodyMusic",
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayLanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "BiblePublicationSection",
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
                    table.PrimaryKey("PK_BiblePublicationSection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublicationSection_BiblePublication_BiblePublicationId",
                        column: x => x.BiblePublicationId,
                        principalTable: "BiblePublication",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationTrack",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    BiblePublicationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublicationTrack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublicationTrack_AudioSource_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BiblePublicationTrack_BiblePublication_BiblePublicationId",
                        column: x => x.BiblePublicationId,
                        principalTable: "BiblePublication",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicTrack",
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
                    table.PrimaryKey("PK_MusicTrack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicTrack_AudioSource_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicTrack_MelodyMusic_MelodyMusicId",
                        column: x => x.MelodyMusicId,
                        principalTable: "MelodyMusic",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicTrack_VocalMusic_VocalMusicId",
                        column: x => x.VocalMusicId,
                        principalTable: "VocalMusic",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BibleTrack",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    BiblePublicationSectionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleTrack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleTrack_AudioSource_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AudioSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BibleTrack_BiblePublicationSection_BiblePublicationSectionId",
                        column: x => x.BiblePublicationSectionId,
                        principalTable: "BiblePublicationSection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioSource_BaseUrlId",
                table: "AudioSource",
                column: "BaseUrlId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationSection_BiblePublicationId_Number",
                table: "BiblePublicationSection",
                columns: new[] { "BiblePublicationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BibleTrack_BiblePublicationSectionId_Number",
                table: "BibleTrack",
                columns: new[] { "BiblePublicationSectionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BibleTrack_SourceId",
                table: "BibleTrack",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublication_Code_LanguageId",
                table: "BiblePublication",
                columns: new[] { "Code", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublication_DisplayLanguageId",
                table: "BiblePublication",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublication_LanguageId",
                table: "BiblePublication",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTrack_BiblePublicationId_Number",
                table: "BiblePublicationTrack",
                columns: new[] { "BiblePublicationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTrack_SourceId",
                table: "BiblePublicationTrack",
                column: "SourceId");

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
                name: "IX_VocalMusic_Code_LanguageId",
                table: "VocalMusic",
                columns: new[] { "Code", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusic_DisplayLanguageId",
                table: "VocalMusic",
                column: "DisplayLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_VocalMusic_LanguageId",
                table: "VocalMusic",
                column: "LanguageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BibleTrack");

            migrationBuilder.DropTable(
                name: "BiblePublicationTrack");

            migrationBuilder.DropTable(
                name: "MusicTrack");

            migrationBuilder.DropTable(
                name: "BiblePublicationSection");

            migrationBuilder.DropTable(
                name: "AudioSource");

            migrationBuilder.DropTable(
                name: "MelodyMusic");

            migrationBuilder.DropTable(
                name: "VocalMusic");

            migrationBuilder.DropTable(
                name: "BiblePublication");

            migrationBuilder.DropTable(
                name: "AudioSourceBaseUrl");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
