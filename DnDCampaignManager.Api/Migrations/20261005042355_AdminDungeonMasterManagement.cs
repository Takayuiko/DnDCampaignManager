using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdminDungeonMasterManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AdminMustBeDM",
                table: "Users",
                sql: "NOT \"IsAdmin\" OR \"Role\" = 'DM'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AdminMustBeDM",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
