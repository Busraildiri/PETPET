using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddNotificationPreferencesAndPushTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileNotificationPreferences",
                schema: "petwork",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CommunityNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    LostPetNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    MatchNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileNotificationPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MobileNotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MobilePushTokens",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilePushTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePushTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePushTokens_Token",
                schema: "petwork",
                table: "MobilePushTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePushTokens_UserId",
                schema: "petwork",
                table: "MobilePushTokens",
                column: "UserId");

            migrationBuilder.Sql("""
                UPDATE petwork."AdoptionListings" AS listing
                SET "Status" = 'adopted'
                WHERE listing."Status" = 'active'
                  AND EXISTS (
                      SELECT 1
                      FROM petwork."AdoptionApplications" AS application
                      WHERE application."AdoptionListingId" = listing."Id"
                        AND application."Status" = 'accepted');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileNotificationPreferences",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "MobilePushTokens",
                schema: "petwork");
        }
    }
}
