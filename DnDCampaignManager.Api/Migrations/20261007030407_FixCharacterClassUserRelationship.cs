using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixCharacterClassUserRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UserId is the ownership key used throughout the application. Do not
            // silently discard a conflicting owner stored in the accidental relationship.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "CharacterClassOptions"
                               WHERE "UserId1" IS NOT NULL AND "UserId1" <> "UserId") THEN
                        RAISE EXCEPTION 'Character class options have conflicting UserId and UserId1 ownership. Resolve these rows before applying FixCharacterClassUserRelationship.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassOptions_Users_UserId1",
                table: "CharacterClassOptions");

            migrationBuilder.DropIndex(
                name: "IX_CharacterClassOptions_UserId1",
                table: "CharacterClassOptions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "CharacterClassOptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "CharacterClassOptions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClassOptions_UserId1",
                table: "CharacterClassOptions",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassOptions_Users_UserId1",
                table: "CharacterClassOptions",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
