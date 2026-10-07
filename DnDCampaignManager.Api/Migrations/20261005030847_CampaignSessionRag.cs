using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class CampaignSessionRag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetrievalInputTokens",
                table: "AIMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RetrievalWarning",
                table: "AIMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcesJson",
                table: "AIMessages",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "CampaignId",
                table: "AIConversations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampaignSessionNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    SessionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PlayedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IndexStatus = table.Column<string>(type: "text", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingInputTokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignSessionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignSessionNotes_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeChunks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionNoteId = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeChunks_CampaignSessionNotes_SessionNoteId",
                        column: x => x.SessionNoteId,
                        principalTable: "CampaignSessionNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_CampaignId",
                table: "AIConversations",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignKnowledgeChunks_SessionNoteId_Position",
                table: "CampaignKnowledgeChunks",
                columns: new[] { "SessionNoteId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSessionNotes_CampaignId_SessionNumber",
                table: "CampaignSessionNotes",
                columns: new[] { "CampaignId", "SessionNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_AIConversations_Campaigns_CampaignId",
                table: "AIConversations",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIConversations_Campaigns_CampaignId",
                table: "AIConversations");

            migrationBuilder.DropTable(
                name: "CampaignKnowledgeChunks");

            migrationBuilder.DropTable(
                name: "CampaignSessionNotes");

            migrationBuilder.DropIndex(
                name: "IX_AIConversations_CampaignId",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "RetrievalInputTokens",
                table: "AIMessages");

            migrationBuilder.DropColumn(
                name: "RetrievalWarning",
                table: "AIMessages");

            migrationBuilder.DropColumn(
                name: "SourcesJson",
                table: "AIMessages");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "AIConversations");
        }
    }
}
