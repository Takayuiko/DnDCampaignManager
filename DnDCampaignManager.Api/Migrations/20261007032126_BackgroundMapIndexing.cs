using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class BackgroundMapIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IndexLeaseId",
                table: "CampaignMaps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexLeaseUntil",
                table: "CampaignMaps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IndexRevision",
                table: "CampaignMaps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndexLeaseId",
                table: "CampaignMaps");

            migrationBuilder.DropColumn(
                name: "IndexLeaseUntil",
                table: "CampaignMaps");

            migrationBuilder.DropColumn(
                name: "IndexRevision",
                table: "CampaignMaps");
        }
    }
}
