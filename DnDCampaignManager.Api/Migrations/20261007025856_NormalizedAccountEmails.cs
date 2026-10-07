using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizedAccountEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Never merge accounts or change ownership to resolve an email collision.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Users" GROUP BY lower(btrim("Email")) HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate normalized account emails exist. Resolve account collisions before applying NormalizedAccountEmails.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(btrim(\"Email\"))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");
        }
    }
}
