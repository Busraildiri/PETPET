using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddAdoptionAndProductReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdoptionListings",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PetName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Species = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Breed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    District = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    HealthInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Story = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdoptionListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdoptionListings_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductReviews",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Experience = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    TasteScore = table.Column<int>(type: "integer", nullable: false),
                    IngredientScore = table.Column<int>(type: "integer", nullable: false),
                    DigestionScore = table.Column<int>(type: "integer", nullable: false),
                    ValueScore = table.Column<int>(type: "integer", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductReviews_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdoptionApplications",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdoptionListingId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdoptionApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdoptionApplications_AdoptionListings_AdoptionListingId",
                        column: x => x.AdoptionListingId,
                        principalSchema: "petwork",
                        principalTable: "AdoptionListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdoptionApplications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdoptionListingReports",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdoptionListingId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdoptionListingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdoptionListingReports_AdoptionListings_AdoptionListingId",
                        column: x => x.AdoptionListingId,
                        principalSchema: "petwork",
                        principalTable: "AdoptionListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdoptionListingReports_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductReviewReports",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductReviewId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductReviewReports_ProductReviews_ProductReviewId",
                        column: x => x.ProductReviewId,
                        principalSchema: "petwork",
                        principalTable: "ProductReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductReviewReports_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionApplications_AdoptionListingId_UserId",
                schema: "petwork",
                table: "AdoptionApplications",
                columns: new[] { "AdoptionListingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionApplications_UserId",
                schema: "petwork",
                table: "AdoptionApplications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionListingReports_AdoptionListingId_UserId",
                schema: "petwork",
                table: "AdoptionListingReports",
                columns: new[] { "AdoptionListingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionListingReports_IsResolved_CreatedAt",
                schema: "petwork",
                table: "AdoptionListingReports",
                columns: new[] { "IsResolved", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionListingReports_UserId",
                schema: "petwork",
                table: "AdoptionListingReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionListings_City_CreatedAt",
                schema: "petwork",
                table: "AdoptionListings",
                columns: new[] { "City", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionListings_IsDeleted_Status_CreatedAt",
                schema: "petwork",
                table: "AdoptionListings",
                columns: new[] { "IsDeleted", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionListings_UserId",
                schema: "petwork",
                table: "AdoptionListings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewReports_IsResolved_CreatedAt",
                schema: "petwork",
                table: "ProductReviewReports",
                columns: new[] { "IsResolved", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewReports_ProductReviewId_UserId",
                schema: "petwork",
                table: "ProductReviewReports",
                columns: new[] { "ProductReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewReports_UserId",
                schema: "petwork",
                table: "ProductReviewReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_IsDeleted_CreatedAt",
                schema: "petwork",
                table: "ProductReviews",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_UserId",
                schema: "petwork",
                table: "ProductReviews",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdoptionApplications",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "AdoptionListingReports",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "ProductReviewReports",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "AdoptionListings",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "ProductReviews",
                schema: "petwork");
        }
    }
}
