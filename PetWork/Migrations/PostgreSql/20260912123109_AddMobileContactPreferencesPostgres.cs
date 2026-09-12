using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddMobileContactPreferencesPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileContactPreferences",
                schema: "petwork",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    AllowPatiMatchSharing = table.Column<bool>(type: "boolean", nullable: false),
                    AllowAdoptionSharing = table.Column<bool>(type: "boolean", nullable: false),
                    AllowLostPetSharing = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileContactPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MobileContactPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileContactPreferences",
                schema: "petwork");
        }
    }
}
