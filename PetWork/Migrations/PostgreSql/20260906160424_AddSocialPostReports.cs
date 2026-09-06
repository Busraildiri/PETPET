using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSocialPostReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialPostReports",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocialPostId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialPostReports_SocialPosts_SocialPostId",
                        column: x => x.SocialPostId,
                        principalSchema: "petwork",
                        principalTable: "SocialPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialPostReports_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostReports_IsResolved_CreatedAt",
                schema: "petwork",
                table: "SocialPostReports",
                columns: new[] { "IsResolved", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostReports_SocialPostId_UserId",
                schema: "petwork",
                table: "SocialPostReports",
                columns: new[] { "SocialPostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostReports_UserId",
                schema: "petwork",
                table: "SocialPostReports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialPostReports",
                schema: "petwork");
        }
    }
}
