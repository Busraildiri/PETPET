using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddProfileDetailsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "petwork",
                table: "Users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasChildren",
                schema: "petwork",
                table: "Users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOtherPets",
                schema: "petwork",
                table: "Users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LivingSituation",
                schema: "petwork",
                table: "Users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                schema: "petwork",
                table: "Users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CareNotes",
                schema: "petwork",
                table: "Pets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Character",
                schema: "petwork",
                table: "Pets",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChildCompatibility",
                schema: "petwork",
                table: "Pets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraAttributesJson",
                schema: "petwork",
                table: "Pets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMicrochipped",
                schema: "petwork",
                table: "Pets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNeutered",
                schema: "petwork",
                table: "Pets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVaccinated",
                schema: "petwork",
                table: "Pets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherPetCompatibility",
                schema: "petwork",
                table: "Pets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsCsv",
                schema: "petwork",
                table: "Pets",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                schema: "petwork",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasChildren",
                schema: "petwork",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasOtherPets",
                schema: "petwork",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LivingSituation",
                schema: "petwork",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Occupation",
                schema: "petwork",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CareNotes",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "Character",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "ChildCompatibility",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "ExtraAttributesJson",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "IsMicrochipped",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "IsNeutered",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "IsVaccinated",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "OtherPetCompatibility",
                schema: "petwork",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "TagsCsv",
                schema: "petwork",
                table: "Pets");
        }
    }
}
