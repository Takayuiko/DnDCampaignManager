using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterClassOptions2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassOption_Users_UserId",
                table: "CharacterClassOption");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CharacterClassOption",
                table: "CharacterClassOption");

            migrationBuilder.RenameTable(
                name: "CharacterClassOption",
                newName: "CharacterClassOptions");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterClassOption_UserId_NormalizedName",
                table: "CharacterClassOptions",
                newName: "IX_CharacterClassOptions_UserId_NormalizedName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CharacterClassOptions",
                table: "CharacterClassOptions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassOptions_Users_UserId",
                table: "CharacterClassOptions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassOptions_Users_UserId",
                table: "CharacterClassOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CharacterClassOptions",
                table: "CharacterClassOptions");

            migrationBuilder.RenameTable(
                name: "CharacterClassOptions",
                newName: "CharacterClassOption");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterClassOptions_UserId_NormalizedName",
                table: "CharacterClassOption",
                newName: "IX_CharacterClassOption_UserId_NormalizedName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CharacterClassOption",
                table: "CharacterClassOption",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassOption_Users_UserId",
                table: "CharacterClassOption",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
