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
                name: "ApiUrls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PathPrefix = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiUrls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

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
                name: "BiblePublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiblePublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiblePublications_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiblePublications_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PublicationLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PublicationCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationLanguages", x => x.Id);
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
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicationLanguageId = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "UrlParams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BiblePublicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    BiblePublicationSectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    BiblePublicationTrackId = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseUrlId = table.Column<int>(type: "INTEGER", nullable: true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsQueryParam = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrlParams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrlParams_ApiUrls_BaseUrlId",
                        column: x => x.BaseUrlId,
                        principalTable: "ApiUrls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UrlParams_BiblePublicationSections_BiblePublicationSectionId",
                        column: x => x.BiblePublicationSectionId,
                        principalTable: "BiblePublicationSections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UrlParams_BiblePublicationTracks_BiblePublicationTrackId",
                        column: x => x.BiblePublicationTrackId,
                        principalTable: "BiblePublicationTracks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UrlParams_BiblePublications_BiblePublicationId",
                        column: x => x.BiblePublicationId,
                        principalTable: "BiblePublications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiUrls_PathPrefix",
                table: "ApiUrls",
                column: "PathPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_ApiUrls_Url",
                table: "ApiUrls",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublications_CategoryId",
                table: "BiblePublications",
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
                name: "IX_PublicationLanguages_LanguageId",
                table: "PublicationLanguages",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationLanguages_PublicationCode_LanguageId",
                table: "PublicationLanguages",
                columns: new[] { "PublicationCode", "LanguageId" },
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
                name: "IX_UrlParams_BaseUrlId",
                table: "UrlParams",
                column: "BaseUrlId");

            migrationBuilder.CreateIndex(
                name: "IX_UrlParams_BiblePublicationId",
                table: "UrlParams",
                column: "BiblePublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_UrlParams_BiblePublicationSectionId",
                table: "UrlParams",
                column: "BiblePublicationSectionId");

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
                name: "ApiUrls");

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
