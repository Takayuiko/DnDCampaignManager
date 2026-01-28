using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDCampaignManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingThrows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SaveChaMiscBonus",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SaveChaProficient",
                table: "Characters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SaveConMiscBonus",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SaveConProficient",
                table: "Characters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SaveDexMiscBonus",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SaveDexProficient",
                table: "Characters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SaveIntMiscBonus",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SaveIntProficient",
                table: "Characters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SaveStrMiscBonus",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SaveStrProficient",
                table: "Characters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SaveWisMiscBonus",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SaveWisProficient",
                table: "Characters",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaveChaMiscBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveChaProficient",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveConMiscBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveConProficient",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveDexMiscBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveDexProficient",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveIntMiscBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveIntProficient",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveStrMiscBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveStrProficient",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveWisMiscBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SaveWisProficient",
                table: "Characters");
        }
    }
}
