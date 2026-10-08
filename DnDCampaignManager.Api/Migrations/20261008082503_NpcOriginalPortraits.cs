using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class NpcOriginalPortraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "OriginalImage",
                table: "CampaignNpcs",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalImageContentType",
                table: "CampaignNpcs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalImage",
                table: "CampaignNpcs");

            migrationBuilder.DropColumn(
                name: "OriginalImageContentType",
                table: "CampaignNpcs");
        }
    }
}
