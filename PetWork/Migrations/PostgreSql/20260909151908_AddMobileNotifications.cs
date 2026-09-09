using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddMobileNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileNotifications",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileNotifications_UserId_IsRead_CreatedAt",
                schema: "petwork",
                table: "MobileNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.Sql("""
                INSERT INTO petwork."MobileNotifications"
                    ("UserId", "Type", "Title", "Body", "EntityType", "EntityId", "IsRead", "CreatedAt")
                SELECT l."UserId", 'lost_sighting', l."PetName" || ' için yeni görülme bildirimi',
                       s."LocationLabel" || ' konumunda yeni bir gözlem paylaşıldı.', 'lost_pet', l."Id", FALSE, s."CreatedAt"
                FROM petwork."LostPetSightings" s
                JOIN petwork."LostPetListings" l ON l."Id" = s."LostPetListingId"
                WHERE NOT s."IsDeleted";

                INSERT INTO petwork."MobileNotifications"
                    ("UserId", "Type", "Title", "Body", "EntityType", "EntityId", "IsRead", "CreatedAt")
                SELECT l."UserId", 'adoption_application', l."PetName" || ' için yeni başvuru',
                       '@' || u."Username" || ' sahiplendirme ilanına başvurdu.', 'adoption', l."Id", FALSE, a."CreatedAt"
                FROM petwork."AdoptionApplications" a
                JOIN petwork."AdoptionListings" l ON l."Id" = a."AdoptionListingId"
                JOIN petwork."Users" u ON u."Id" = a."UserId";

                INSERT INTO petwork."MobileNotifications"
                    ("UserId", "Type", "Title", "Body", "EntityType", "EntityId", "IsRead", "CreatedAt")
                SELECT a."UserId", 'adoption_status', l."PetName" || ' başvurun güncellendi',
                       CASE WHEN a."Status" = 'accepted' THEN 'Sahiplendirme başvurun kabul edildi.' ELSE 'Sahiplendirme başvurun reddedildi.' END,
                       'adoption', l."Id", FALSE, LOCALTIMESTAMP
                FROM petwork."AdoptionApplications" a
                JOIN petwork."AdoptionListings" l ON l."Id" = a."AdoptionListingId"
                WHERE a."Status" IN ('accepted', 'rejected');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileNotifications",
                schema: "petwork");
        }
    }
}
