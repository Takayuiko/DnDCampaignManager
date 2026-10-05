using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class OneChatPerCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the oldest chat ID and move all existing messages into it before enforcing uniqueness.
            // General chats are retained as inaccessible legacy data.
            migrationBuilder.Sql("""
                LOCK TABLE "AIConversations", "AIMessages" IN SHARE ROW EXCLUSIVE MODE;
                CREATE TEMP TABLE campaign_chat_merge ON COMMIT DROP AS
                SELECT "Id", FIRST_VALUE("Id") OVER (
                    PARTITION BY "UserId", "CampaignId" ORDER BY "CreatedAtUtc", "Id") AS "KeepId"
                FROM "AIConversations" WHERE "CampaignId" IS NOT NULL;
                UPDATE "AIMessages" AS m SET "ConversationId" = merge."KeepId"
                FROM campaign_chat_merge AS merge
                WHERE m."ConversationId" = merge."Id" AND merge."Id" <> merge."KeepId";
                UPDATE "AIConversations" AS c SET "UpdatedAtUtc" = latest.updated
                FROM (SELECT merge."KeepId", MAX(original."UpdatedAtUtc") AS updated
                    FROM campaign_chat_merge AS merge JOIN "AIConversations" AS original ON original."Id" = merge."Id"
                    GROUP BY merge."KeepId") AS latest WHERE c."Id" = latest."KeepId";
                DELETE FROM "AIConversations" AS c USING campaign_chat_merge AS merge
                WHERE c."Id" = merge."Id" AND merge."Id" <> merge."KeepId";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_UserId_CampaignId",
                table: "AIConversations",
                columns: new[] { "UserId", "CampaignId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AIConversations_UserId_CampaignId",
                table: "AIConversations");
        }
    }
}
