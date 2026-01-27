using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScopeCharacterClassOptionsToCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampaignId",
                table: "CharacterClassOptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "CharacterClassOptions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClassOptions_CampaignId",
                table: "CharacterClassOptions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClassOptions_UserId1",
                table: "CharacterClassOptions",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassOptions_Campaigns_CampaignId",
                table: "CharacterClassOptions",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassOptions_Users_UserId1",
                table: "CharacterClassOptions",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassOptions_Campaigns_CampaignId",
                table: "CharacterClassOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassOptions_Users_UserId1",
                table: "CharacterClassOptions");

            migrationBuilder.DropIndex(
                name: "IX_CharacterClassOptions_CampaignId",
                table: "CharacterClassOptions");

            migrationBuilder.DropIndex(
                name: "IX_CharacterClassOptions_UserId1",
                table: "CharacterClassOptions");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "CharacterClassOptions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "CharacterClassOptions");
        }
    }
}
