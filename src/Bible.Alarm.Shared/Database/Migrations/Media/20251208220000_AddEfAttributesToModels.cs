using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bible.Alarm.Shared.Database.Migrations.Media;

/// <inheritdoc />
public partial class AddEfAttributesToModels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add unique index for Language Code
        migrationBuilder.CreateIndex(
            name: "IX_Languages_Code",
            table: "Languages",
            column: "Code",
            unique: true);

        // Add unique composite index for BibleTranslation (Code + LanguageId)
        migrationBuilder.CreateIndex(
            name: "IX_BibleTranslations_Code_LanguageId",
            table: "BibleTranslations",
            columns: new[] { "Code", "LanguageId" },
            unique: true);

        // Add unique composite index for BibleBook (BibleTranslationId + Number)
        migrationBuilder.CreateIndex(
            name: "IX_BibleBook_BibleTranslationId_Number",
            table: "BibleBook",
            columns: new[] { "BibleTranslationId", "Number" },
            unique: true);

        // Add unique composite index for BibleChapter (BibleBookId + Number)
        migrationBuilder.CreateIndex(
            name: "IX_BibleChapter_BibleBookId_Number",
            table: "BibleChapter",
            columns: new[] { "BibleBookId", "Number" },
            unique: true);

        // Add unique index for MelodyMusic Code
        migrationBuilder.CreateIndex(
            name: "IX_MelodyMusic_Code",
            table: "MelodyMusic",
            column: "Code",
            unique: true);

        // Add unique composite index for VocalMusic (Code + LanguageId)
        migrationBuilder.CreateIndex(
            name: "IX_VocalMusic_Code_LanguageId",
            table: "VocalMusic",
            columns: new[] { "Code", "LanguageId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VocalMusic_Code_LanguageId",
            table: "VocalMusic");

        migrationBuilder.DropIndex(
            name: "IX_MelodyMusic_Code",
            table: "MelodyMusic");

        migrationBuilder.DropIndex(
            name: "IX_BibleChapter_BibleBookId_Number",
            table: "BibleChapter");

        migrationBuilder.DropIndex(
            name: "IX_BibleBook_BibleTranslationId_Number",
            table: "BibleBook");

        migrationBuilder.DropIndex(
            name: "IX_BibleTranslations_Code_LanguageId",
            table: "BibleTranslations");

        migrationBuilder.DropIndex(
            name: "IX_Languages_Code",
            table: "Languages");
    }
}

