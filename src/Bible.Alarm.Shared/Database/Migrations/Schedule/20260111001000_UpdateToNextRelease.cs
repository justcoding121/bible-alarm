using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bible.Alarm.Shared.Database.Migrations.Schedule
{
    /// <inheritdoc />
    public partial class UpdateToNextRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename BibleReadingSchedules table to BiblePublicationSchedules
            migrationBuilder.RenameTable(
                name: "BibleReadingSchedules",
                newName: "BiblePublicationSchedules");

            // Rename BookNumber to SectionNumber and make it nullable
            migrationBuilder.RenameColumn(
                name: "BookNumber",
                table: "BiblePublicationSchedules",
                newName: "SectionNumber");

            migrationBuilder.AlterColumn<int>(
                name: "SectionNumber",
                table: "BiblePublicationSchedules",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // Rename ChapterNumber to TrackNumber
            migrationBuilder.RenameColumn(
                name: "ChapterNumber",
                table: "BiblePublicationSchedules",
                newName: "TrackNumber");

            // Rename NumberOfChaptersToRead to NumberOfTracksToRead
            migrationBuilder.RenameColumn(
                name: "NumberOfChaptersToRead",
                table: "AlarmSchedules",
                newName: "NumberOfTracksToRead");

            // Rename AlarmMusic table to AlarmMusics
            migrationBuilder.RenameTable(
                name: "AlarmMusic",
                newName: "AlarmMusics");

            // Rename existing index for AlarmMusics (only IX_AlarmMusic_AlarmScheduleId exists from InitialCreate)
            migrationBuilder.RenameIndex(
                name: "IX_AlarmMusic_AlarmScheduleId",
                table: "AlarmMusics",
                newName: "IX_AlarmMusics_AlarmScheduleId");

            // Add MaxLength constraints
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AlarmSchedules",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublicationCode",
                table: "BiblePublicationSchedules",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "BiblePublicationSchedules",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublicationCode",
                table: "AlarmMusics",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "AlarmMusics",
                type: "TEXT",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "GeneralSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            // Add indexes
            migrationBuilder.CreateIndex(
                name: "IX_AlarmSchedules_IsEnabled",
                table: "AlarmSchedules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmSchedules_Hour_Minute",
                table: "AlarmSchedules",
                columns: new[] { "Hour", "Minute" });

            // Create new index (this index didn't exist before, so we create it)
            migrationBuilder.CreateIndex(
                name: "IX_AlarmMusics_PublicationCode_LanguageCode",
                table: "AlarmMusics",
                columns: new[] { "PublicationCode", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_ScheduledTime",
                table: "AlarmNotifications",
                column: "ScheduledTime");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_Sent_Fired",
                table: "AlarmNotifications",
                columns: new[] { "Sent", "Fired" });

            migrationBuilder.CreateIndex(
                name: "IX_BiblePublicationSchedules_PublicationCode_LanguageCode",
                table: "BiblePublicationSchedules",
                columns: new[] { "PublicationCode", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralSettings_Key",
                table: "GeneralSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove indexes
            migrationBuilder.DropIndex(
                name: "IX_GeneralSettings_Key",
                table: "GeneralSettings");

            migrationBuilder.DropIndex(
                name: "IX_BiblePublicationSchedules_PublicationCode_LanguageCode",
                table: "BiblePublicationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AlarmNotifications_Sent_Fired",
                table: "AlarmNotifications");

            migrationBuilder.DropIndex(
                name: "IX_AlarmNotifications_ScheduledTime",
                table: "AlarmNotifications");

            migrationBuilder.DropIndex(
                name: "IX_AlarmMusics_PublicationCode_LanguageCode",
                table: "AlarmMusics");

            migrationBuilder.DropIndex(
                name: "IX_AlarmSchedules_Hour_Minute",
                table: "AlarmSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AlarmSchedules_IsEnabled",
                table: "AlarmSchedules");

            // Revert MaxLength constraints
            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "GeneralSettings",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255,
                oldNullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "AlarmMusics",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublicationCode",
                table: "AlarmMusics",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "BiblePublicationSchedules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldNullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "PublicationCode",
                table: "BiblePublicationSchedules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AlarmSchedules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255,
                oldNullable: false);

            // Drop the index that was created in this migration
            migrationBuilder.DropIndex(
                name: "IX_AlarmMusics_PublicationCode_LanguageCode",
                table: "AlarmMusics");

            // Rename index back for AlarmMusic
            migrationBuilder.RenameIndex(
                name: "IX_AlarmMusics_AlarmScheduleId",
                table: "AlarmMusics",
                newName: "IX_AlarmMusic_AlarmScheduleId");

            // Rename AlarmMusics table back to AlarmMusic
            migrationBuilder.RenameTable(
                name: "AlarmMusics",
                newName: "AlarmMusic");

            // Rename columns back
            migrationBuilder.RenameColumn(
                name: "NumberOfTracksToRead",
                table: "AlarmSchedules",
                newName: "NumberOfChaptersToRead");

            migrationBuilder.RenameColumn(
                name: "TrackNumber",
                table: "BiblePublicationSchedules",
                newName: "ChapterNumber");

            migrationBuilder.AlterColumn<int>(
                name: "SectionNumber",
                table: "BiblePublicationSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "SectionNumber",
                table: "BiblePublicationSchedules",
                newName: "BookNumber");

            // Rename table back
            migrationBuilder.RenameTable(
                name: "BiblePublicationSchedules",
                newName: "BibleReadingSchedules");
        }
    }
}
