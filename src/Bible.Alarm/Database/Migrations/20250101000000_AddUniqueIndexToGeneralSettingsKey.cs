using Microsoft.EntityFrameworkCore.Migrations;

namespace Bible.Alarm.Database.Migrations
{
    public partial class AddUniqueIndexToGeneralSettingsKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create unique index on Key column to enforce uniqueness
            // This ensures that each setting key can only exist once in the database
            migrationBuilder.CreateIndex(
                name: "IX_GeneralSettings_Key",
                table: "GeneralSettings",
                column: "Key",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index when rolling back
            migrationBuilder.DropIndex(
                name: "IX_GeneralSettings_Key",
                table: "GeneralSettings");
        }
    }
}
