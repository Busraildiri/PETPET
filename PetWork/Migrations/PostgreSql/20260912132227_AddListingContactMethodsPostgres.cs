using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddListingContactMethodsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowInAppMessages",
                schema: "petwork",
                table: "LostPetListings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "petwork",
                table: "LostPetListings",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "petwork",
                table: "LostPetListings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowInAppMessages",
                schema: "petwork",
                table: "AdoptionListings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "petwork",
                table: "AdoptionListings",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "petwork",
                table: "AdoptionListings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowInAppMessages",
                schema: "petwork",
                table: "LostPetListings");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "petwork",
                table: "LostPetListings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "petwork",
                table: "LostPetListings");

            migrationBuilder.DropColumn(
                name: "AllowInAppMessages",
                schema: "petwork",
                table: "AdoptionListings");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "petwork",
                table: "AdoptionListings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "petwork",
                table: "AdoptionListings");
        }
    }
}
