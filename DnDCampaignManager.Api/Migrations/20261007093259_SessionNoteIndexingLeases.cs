using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class SessionNoteIndexingLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IndexLeaseId",
                table: "CampaignSessionNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexLeaseUntil",
                table: "CampaignSessionNotes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndexLeaseId",
                table: "CampaignSessionNotes");

            migrationBuilder.DropColumn(
                name: "IndexLeaseUntil",
                table: "CampaignSessionNotes");
        }
    }
}
