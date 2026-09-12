using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddPatiMatchContactSharesPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatiMatchContactShares",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PetOneId = table.Column<int>(type: "integer", nullable: false),
                    PetTwoId = table.Column<int>(type: "integer", nullable: false),
                    SharedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    SharedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatiMatchContactShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatiMatchContactShares_Pets_PetOneId",
                        column: x => x.PetOneId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchContactShares_Pets_PetTwoId",
                        column: x => x.PetTwoId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchContactShares_Users_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchContactShares_PetOneId_PetTwoId_SharedByUserId",
                schema: "petwork",
                table: "PatiMatchContactShares",
                columns: new[] { "PetOneId", "PetTwoId", "SharedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchContactShares_PetTwoId",
                schema: "petwork",
                table: "PatiMatchContactShares",
                column: "PetTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchContactShares_SharedByUserId",
                schema: "petwork",
                table: "PatiMatchContactShares",
                column: "SharedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatiMatchContactShares",
                schema: "petwork");
        }
    }
}
