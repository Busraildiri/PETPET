using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddPatiMatchMessagingPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatiMatchDecisions",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourcePetId = table.Column<int>(type: "integer", nullable: false),
                    TargetPetId = table.Column<int>(type: "integer", nullable: false),
                    IsLike = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatiMatchDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatiMatchDecisions_Pets_SourcePetId",
                        column: x => x.SourcePetId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchDecisions_Pets_TargetPetId",
                        column: x => x.TargetPetId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatiMatchMessages",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PetOneId = table.Column<int>(type: "integer", nullable: false),
                    PetTwoId = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatiMatchMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatiMatchMessages_Pets_PetOneId",
                        column: x => x.PetOneId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchMessages_Pets_PetTwoId",
                        column: x => x.PetTwoId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatiMatchMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatiMatchProfiles",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PetId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    District = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PreferredTypesCsv = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatiMatchProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatiMatchProfiles_Pets_PetId",
                        column: x => x.PetId,
                        principalSchema: "petwork",
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchDecisions_SourcePetId_TargetPetId",
                schema: "petwork",
                table: "PatiMatchDecisions",
                columns: new[] { "SourcePetId", "TargetPetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchDecisions_TargetPetId",
                schema: "petwork",
                table: "PatiMatchDecisions",
                column: "TargetPetId");

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchMessages_PetOneId_PetTwoId_CreatedAt",
                schema: "petwork",
                table: "PatiMatchMessages",
                columns: new[] { "PetOneId", "PetTwoId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchMessages_PetTwoId",
                schema: "petwork",
                table: "PatiMatchMessages",
                column: "PetTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchMessages_SenderUserId",
                schema: "petwork",
                table: "PatiMatchMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchProfiles_IsActive_City",
                schema: "petwork",
                table: "PatiMatchProfiles",
                columns: new[] { "IsActive", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_PatiMatchProfiles_PetId",
                schema: "petwork",
                table: "PatiMatchProfiles",
                column: "PetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatiMatchDecisions",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "PatiMatchMessages",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "PatiMatchProfiles",
                schema: "petwork");
        }
    }
}
