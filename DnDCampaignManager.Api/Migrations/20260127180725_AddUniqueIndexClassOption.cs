using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexClassOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CharacterClassOptions_UserId_NormalizedName",
                table: "CharacterClassOptions");

            migrationBuilder.DropColumn(
                name: "DeathSaveFailures",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "DeathSaveSuccesses",
                table: "Characters");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClassOptions_UserId_NormalizedName_CampaignId",
                table: "CharacterClassOptions",
                columns: new[] { "UserId", "NormalizedName", "CampaignId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CharacterClassOptions_UserId_NormalizedName_CampaignId",
                table: "CharacterClassOptions");

            migrationBuilder.AddColumn<int>(
                name: "DeathSaveFailures",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeathSaveSuccesses",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClassOptions_UserId_NormalizedName",
                table: "CharacterClassOptions",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);
        }
    }
}
