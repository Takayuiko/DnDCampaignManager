using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class CampaignNpcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignNpcs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsPartyMember = table.Column<bool>(type: "boolean", nullable: false),
                    MapId = table.Column<long>(type: "bigint", nullable: true),
                    LocationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignNpcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignNpcs_CampaignMaps_MapId",
                        column: x => x.MapId,
                        principalTable: "CampaignMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CampaignNpcs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignNpcs_CampaignId_Name",
                table: "CampaignNpcs",
                columns: new[] { "CampaignId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignNpcs_MapId",
                table: "CampaignNpcs",
                column: "MapId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignNpcs");
        }
    }
}
