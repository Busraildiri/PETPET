using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations
{
    /// <inheritdoc />
    public partial class AddListingContactMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowInAppMessages",
                table: "LostPetListings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "LostPetListings",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "LostPetListings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowInAppMessages",
                table: "AdoptionListings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "AdoptionListings",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "AdoptionListings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowInAppMessages",
                table: "LostPetListings");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "LostPetListings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "LostPetListings");

            migrationBuilder.DropColumn(
                name: "AllowInAppMessages",
                table: "AdoptionListings");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "AdoptionListings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "AdoptionListings");
        }
    }
}
