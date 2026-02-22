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
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryNamesByLanguage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryNamesByLanguage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryNamesByLanguage_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Direction = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LanguageNamesByLanguage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayLanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageNamesByLanguage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageNamesByLanguage_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMusic = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PublicationCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublications_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationCategories",
                columns: table => new
                {
                    BiblePublicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublicationCategories", x => new { x.BiblePublicationId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_BiblePublicationCategories_BiblePublications_BiblePublicationId",
                        column: x => x.BiblePublicationId,
                        principalTable: "BiblePublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiblePublicationCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicationLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PublicationCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: true),
                    HarvestType = table.Column<int>(type: "INTEGER", nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationLanguages_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicationLanguages_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SectionCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
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
                name: "SectionLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PublicationCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SectionCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: true),
                    PublicationLanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    HarvestType = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionLanguages_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionLanguages_PublicationLanguages_PublicationLanguageId",
                        column: x => x.PublicationLanguageId,
                        principalTable: "PublicationLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiblePublicationTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "UrlParams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BiblePublicationTrackId = table.Column<int>(type: "INTEGER", nullable: true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsQueryParam = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrlParams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrlParams_BiblePublicationTracks_BiblePublicationTrackId",
                        column: x => x.BiblePublicationTrackId,
                        principalTable: "BiblePublicationTracks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationCategories_CategoryId",
                table: "BiblePublicationCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublications_LanguageId",
                table: "BiblePublications",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationSections_BiblePublicationId",
                table: "BiblePublicationSections",
                column: "BiblePublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTracks_BiblePublicationId_TrackCode",
                table: "BiblePublicationTracks",
                columns: new[] { "BiblePublicationId", "TrackCode" });

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationTracks_BiblePublicationSectionId_TrackCode",
                table: "BiblePublicationTracks",
                columns: new[] { "BiblePublicationSectionId", "TrackCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryCode",
                table: "Categories",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryNamesByLanguage_CategoryId_LanguageCode",
                table: "CategoryNamesByLanguage",
                columns: new[] { "CategoryId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageNamesByLanguage_LanguageId_DisplayLanguageCode",
                table: "LanguageNamesByLanguage",
                columns: new[] { "LanguageId", "DisplayLanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Languages_LanguageCode",
                table: "Languages",
                column: "LanguageCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationLanguages_CategoryId",
                table: "PublicationLanguages",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationLanguages_LanguageId",
                table: "PublicationLanguages",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationLanguages_PublicationCode_LanguageId_CategoryId",
                table: "PublicationLanguages",
                columns: new[] { "PublicationCode", "LanguageId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionLanguages_LanguageId",
                table: "SectionLanguages",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionLanguages_PublicationCode_SectionCode_LanguageId",
                table: "SectionLanguages",
                columns: new[] { "PublicationCode", "SectionCode", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionLanguages_PublicationLanguageId",
                table: "SectionLanguages",
                column: "PublicationLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_UrlParams_BiblePublicationTrackId",
                table: "UrlParams",
                column: "BiblePublicationTrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionLanguages");

            migrationBuilder.DropTable(
                name: "UrlParams");

            migrationBuilder.DropTable(
                name: "PublicationLanguages");

            migrationBuilder.DropTable(
                name: "BiblePublicationCategories");

            migrationBuilder.DropTable(
                name: "CategoryNamesByLanguage");

            migrationBuilder.DropTable(
                name: "LanguageNamesByLanguage");

            migrationBuilder.DropTable(
                name: "BiblePublicationTracks");

            migrationBuilder.DropTable(
                name: "BiblePublicationSections");

            migrationBuilder.DropTable(
                name: "BiblePublications");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
