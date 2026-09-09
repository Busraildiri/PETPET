using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSocialReactionsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialCommentLikes",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocialCommentId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialCommentLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialCommentLikes_SocialComments_SocialCommentId",
                        column: x => x.SocialCommentId,
                        principalSchema: "petwork",
                        principalTable: "SocialComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialCommentLikes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SocialPostLikes",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocialPostId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialPostLikes_SocialPosts_SocialPostId",
                        column: x => x.SocialPostId,
                        principalSchema: "petwork",
                        principalTable: "SocialPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialPostLikes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SocialPostSaves",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocialPostId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostSaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialPostSaves_SocialPosts_SocialPostId",
                        column: x => x.SocialPostId,
                        principalSchema: "petwork",
                        principalTable: "SocialPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialPostSaves_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialCommentLikes_SocialCommentId_UserId",
                schema: "petwork",
                table: "SocialCommentLikes",
                columns: new[] { "SocialCommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialCommentLikes_UserId",
                schema: "petwork",
                table: "SocialCommentLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostLikes_SocialPostId_UserId",
                schema: "petwork",
                table: "SocialPostLikes",
                columns: new[] { "SocialPostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostLikes_UserId",
                schema: "petwork",
                table: "SocialPostLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostSaves_SocialPostId_UserId",
                schema: "petwork",
                table: "SocialPostSaves",
                columns: new[] { "SocialPostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostSaves_UserId",
                schema: "petwork",
                table: "SocialPostSaves",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialCommentLikes",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "SocialPostLikes",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "SocialPostSaves",
                schema: "petwork");
        }
    }
}
