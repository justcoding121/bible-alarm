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

            // Rename BookNumber to SectionCode and change type to string to match media index db
            migrationBuilder.RenameColumn(
                name: "BookNumber",
                table: "BiblePublicationSchedules",
                newName: "SectionCode");

            migrationBuilder.AlterColumn<string>(
                name: "SectionCode",
                table: "BiblePublicationSchedules",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // Rename ChapterNumber to TrackNumber
            migrationBuilder.RenameColumn(
                name: "ChapterNumber",
                table: "BiblePublicationSchedules",
                newName: "TrackNumber");

            // Rename NumberOfChaptersToRead to NumberOfTracksToPlay
            migrationBuilder.RenameColumn(
                name: "NumberOfChaptersToRead",
                table: "AlarmSchedules",
                newName: "NumberOfTracksToPlay");

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
                table: "AlarmMusic",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "AlarmMusic",
                type: "TEXT",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            // Add SectionCode column to AlarmMusic for music publications with sections
            migrationBuilder.AddColumn<string>(
                name: "SectionCode",
                table: "AlarmMusic",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_AlarmMusic_PublicationCode_LanguageCode",
                table: "AlarmMusic",
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

            // Disable music for all existing alarms to ensure users re-select with new SectionCode logic
            migrationBuilder.Sql("UPDATE AlarmSchedules SET MusicEnabled = 0 WHERE MusicEnabled = 1");
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
                name: "IX_AlarmMusic_PublicationCode_LanguageCode",
                table: "AlarmMusic");

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

            // Remove SectionCode column from AlarmMusic
            migrationBuilder.DropColumn(
                name: "SectionCode",
                table: "AlarmMusic");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "AlarmMusic",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublicationCode",
                table: "AlarmMusic",
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

            // Rename columns back
            migrationBuilder.RenameColumn(
                name: "NumberOfTracksToPlay",
                table: "AlarmSchedules",
                newName: "NumberOfChaptersToRead");

            migrationBuilder.RenameColumn(
                name: "TrackNumber",
                table: "BiblePublicationSchedules",
                newName: "ChapterNumber");

            migrationBuilder.AlterColumn<int>(
                name: "SectionCode",
                table: "BiblePublicationSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "SectionCode",
                table: "BiblePublicationSchedules",
                newName: "BookNumber");

            // Rename table back
            migrationBuilder.RenameTable(
                name: "BiblePublicationSchedules",
                newName: "BibleReadingSchedules");
        }
    }
}
