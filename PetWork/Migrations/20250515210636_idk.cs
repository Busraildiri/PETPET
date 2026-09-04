using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations
{
    /// <inheritdoc />
    public partial class MigrationIdk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the missing columns to the Pets table
            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Pets",
                type: "nvarchar(max)",
                nullable: true);
                
            migrationBuilder.AddColumn<string>(
                name: "PetType",
                table: "Pets",
                type: "nvarchar(max)",
                nullable: true);
                
            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Pets",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Pets");
                
            migrationBuilder.DropColumn(
                name: "PetType",
                table: "Pets");
                
            migrationBuilder.DropColumn(
                name: "Age",
                table: "Pets");
        }
    }
}
