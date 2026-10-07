using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class CampaignItemsAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Characters_Id_CampaignId",
                table: "Characters",
                columns: new[] { "Id", "CampaignId" });

            migrationBuilder.CreateTable(
                name: "CampaignItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WeightLb = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CostGp = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignItems", x => x.Id);
                    table.UniqueConstraint("AK_CampaignItems_Id_CampaignId", x => new { x.Id, x.CampaignId });
                    table.CheckConstraint("CK_CampaignItems_Cost", "\"CostGp\" IS NULL OR \"CostGp\" >= 0");
                    table.CheckConstraint("CK_CampaignItems_Weight", "\"WeightLb\" IS NULL OR \"WeightLb\" >= 0");
                    table.ForeignKey(
                        name: "FK_CampaignItems_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterItems", x => x.Id);
                    table.CheckConstraint("CK_CharacterItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CharacterItems_CampaignItems_ItemId_CampaignId",
                        columns: x => new { x.ItemId, x.CampaignId },
                        principalTable: "CampaignItems",
                        principalColumns: new[] { "Id", "CampaignId" });
                    table.ForeignKey(
                        name: "FK_CharacterItems_Characters_CharacterId_CampaignId",
                        columns: x => new { x.CharacterId, x.CampaignId },
                        principalTable: "Characters",
                        principalColumns: new[] { "Id", "CampaignId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignItems_CampaignId_NormalizedName",
                table: "CampaignItems",
                columns: new[] { "CampaignId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterItems_CharacterId_CampaignId",
                table: "CharacterItems",
                columns: new[] { "CharacterId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterItems_ItemId_CampaignId",
                table: "CharacterItems",
                columns: new[] { "ItemId", "CampaignId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterItems");

            migrationBuilder.DropTable(
                name: "CampaignItems");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Characters_Id_CampaignId",
                table: "Characters");
        }
    }
}
