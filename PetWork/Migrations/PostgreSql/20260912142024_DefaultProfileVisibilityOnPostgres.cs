using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class DefaultProfileVisibilityOnPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowBioToOthers",
                schema: "petwork",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPetsToOthers",
                schema: "petwork",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublic",
                schema: "petwork",
                table: "Pets",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.Sql("UPDATE petwork.\"Pets\" SET \"IsPublic\" = TRUE;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowBioToOthers",
                schema: "petwork",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowPetsToOthers",
                schema: "petwork",
                table: "Users");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublic",
                schema: "petwork",
                table: "Pets",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
