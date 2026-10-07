using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCampaignMapNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampaignMaps_CampaignId",
                table: "CampaignMaps");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                table: "CampaignMaps",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            // Preserve every map. Keep the oldest duplicate's name and suffix newer duplicates.
            // Reserve existing names first so a generated suffix cannot collide with another map.
            migrationBuilder.Sql("""
                UPDATE "CampaignMaps" SET "NormalizedTitle" = upper(btrim("Title"));
                DO $$
                DECLARE
                    duplicate record;
                    candidate text;
                    attempt integer;
                BEGIN
                    FOR duplicate IN
                        SELECT * FROM (
                            SELECT "Id", "CampaignId", "Title", row_number() OVER (
                                PARTITION BY "CampaignId", "NormalizedTitle" ORDER BY "Id") AS position
                            FROM "CampaignMaps"
                        ) ranked WHERE position > 1 ORDER BY "Id"
                    LOOP
                        attempt := 0;
                        LOOP
                            candidate := left(btrim(duplicate."Title"), 110) || ' (duplicate ' || duplicate."Id" ||
                                CASE WHEN attempt = 0 THEN '' ELSE '-' || attempt END || ')';
                            EXIT WHEN NOT EXISTS (SELECT 1 FROM "CampaignMaps" WHERE "CampaignId" = duplicate."CampaignId"
                                AND "Id" <> duplicate."Id" AND "NormalizedTitle" = upper(candidate));
                            attempt := attempt + 1;
                        END LOOP;
                        UPDATE "CampaignMaps" SET "Title" = candidate, "NormalizedTitle" = upper(candidate),
                            "IndexStatus" = 'pending' WHERE "Id" = duplicate."Id";
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMaps_CampaignId_NormalizedTitle",
                table: "CampaignMaps",
                columns: new[] { "CampaignId", "NormalizedTitle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampaignMaps_CampaignId_NormalizedTitle",
                table: "CampaignMaps");

            migrationBuilder.DropColumn(
                name: "NormalizedTitle",
                table: "CampaignMaps");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMaps_CampaignId",
                table: "CampaignMaps",
                column: "CampaignId");
        }
    }
}
