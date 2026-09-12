using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations
{
    /// <inheritdoc />
    public partial class AddPatiMatchContactShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatiMatchContactShares",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PetOneId = table.Column<int>(type: "int", nullable: false),
                    PetTwoId = table.Column<int>(type: "int", nullable: false),
                    SharedByUserId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatiMatchContactShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatiMatchContactShares_Pets_PetOneId",
                        column: x => x.PetOneId,
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchContactShares_Pets_PetTwoId",
                        column: x => x.PetTwoId,
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchContactShares_Users_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchContactShares_PetOneId_PetTwoId_SharedByUserId",
                table: "PatiMatchContactShares",
                columns: new[] { "PetOneId", "PetTwoId", "SharedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchContactShares_PetTwoId",
                table: "PatiMatchContactShares",
                column: "PetTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchContactShares_SharedByUserId",
                table: "PatiMatchContactShares",
                column: "SharedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatiMatchContactShares");
        }
    }
}
