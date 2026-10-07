using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    public partial class HashedRefreshTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn("Token", "RefreshTokens", "TokenHash");
            migrationBuilder.RenameColumn("ReplacedByToken", "RefreshTokens", "ReplacedByTokenHash");
            // PostgreSQL's built-in SHA-256 needs no extension. Match UTF-8 and
            // lowercase hexadecimal used by RefreshTokenService.Hash.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" SET
                    "TokenHash" = encode(sha256(convert_to("TokenHash", 'UTF8')), 'hex'),
                    "ReplacedByTokenHash" = CASE WHEN "ReplacedByTokenHash" IS NULL THEN NULL
                        ELSE encode(sha256(convert_to("ReplacedByTokenHash", 'UTF8')), 'hex') END;
                """);
            migrationBuilder.AlterColumn<string>("TokenHash", "RefreshTokens", type: "character varying(64)",
                maxLength: 64, nullable: false, oldClrType: typeof(string), oldType: "text");
            migrationBuilder.AlterColumn<string>("ReplacedByTokenHash", "RefreshTokens", type: "character varying(64)",
                maxLength: 64, nullable: true, oldClrType: typeof(string), oldType: "text", oldNullable: true);
            migrationBuilder.CreateIndex("IX_RefreshTokens_TokenHash", "RefreshTokens", "TokenHash", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hashes are irreversible. Preserve audit rows but require a new login
            // after rollback instead of treating a stored hash as a bearer token.
            migrationBuilder.Sql("UPDATE \"RefreshTokens\" SET \"IsRevoked\" = TRUE;");
            migrationBuilder.DropIndex("IX_RefreshTokens_TokenHash", "RefreshTokens");
            migrationBuilder.AlterColumn<string>("TokenHash", "RefreshTokens", type: "text", nullable: false,
                oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64);
            migrationBuilder.AlterColumn<string>("ReplacedByTokenHash", "RefreshTokens", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64, oldNullable: true);
            migrationBuilder.RenameColumn("TokenHash", "RefreshTokens", "Token");
            migrationBuilder.RenameColumn("ReplacedByTokenHash", "RefreshTokens", "ReplacedByToken");
        }
    }
}