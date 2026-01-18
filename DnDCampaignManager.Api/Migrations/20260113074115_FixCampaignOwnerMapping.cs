using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixCampaignOwnerMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_Users_DmId",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_DmId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "DmId",
                table: "Campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_OwnerId",
                table: "Campaigns",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_Users_OwnerId",
                table: "Campaigns",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_Users_OwnerId",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_OwnerId",
                table: "Campaigns");

            migrationBuilder.AddColumn<int>(
                name: "DmId",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_DmId",
                table: "Campaigns",
                column: "DmId");

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_Users_DmId",
                table: "Campaigns",
                column: "DmId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
