using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bible.Alarm.Shared.Database.Migrations.Media
{
    /// <inheritdoc />
    public partial class AddHarvestTypeToSectionLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make HarvestType nullable in PublicationLanguages
            migrationBuilder.AlterColumn<int>(
                name: "HarvestType",
                table: "PublicationLanguages",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // Add HarvestType column to SectionLanguages
            migrationBuilder.AddColumn<int>(
                name: "HarvestType",
                table: "SectionLanguages",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove HarvestType column from SectionLanguages
            migrationBuilder.DropColumn(
                name: "HarvestType",
                table: "SectionLanguages");

            // Make HarvestType required again in PublicationLanguages
            migrationBuilder.AlterColumn<int>(
                name: "HarvestType",
                table: "PublicationLanguages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
