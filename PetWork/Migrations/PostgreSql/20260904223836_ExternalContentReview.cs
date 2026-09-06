using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class ExternalContentReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalContentSources",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LocalContentId = table.Column<int>(type: "integer", nullable: true),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentSourceId = table.Column<int>(type: "integer", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ApiUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceAuthorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceAuthorUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LicenseCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LicenseUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OriginalPublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SourceUpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OriginalContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: false),
                    TranslatedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TranslatedText = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TranslationProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TranslationVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TranslatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WasTranslated = table.Column<bool>(type: "boolean", nullable: false),
                    WasModified = table.Column<bool>(type: "boolean", nullable: false),
                    AttributionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsSourceAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalContentSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalContentSources_ExternalContentSources_ParentSourceId",
                        column: x => x.ParentSourceId,
                        principalSchema: "petwork",
                        principalTable: "ExternalContentSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalContentSources_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ContentImportAudits",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalContentSourceId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentImportAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentImportAudits_ExternalContentSources_ExternalContentS~",
                        column: x => x.ExternalContentSourceId,
                        principalSchema: "petwork",
                        principalTable: "ExternalContentSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentImportAudits_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportAudits_ExternalContentSourceId",
                schema: "petwork",
                table: "ContentImportAudits",
                column: "ExternalContentSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportAudits_PerformedByUserId",
                schema: "petwork",
                table: "ContentImportAudits",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalContentSources_ParentSourceId",
                schema: "petwork",
                table: "ExternalContentSources",
                column: "ParentSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalContentSources_Provider_ExternalId_ContentType",
                schema: "petwork",
                table: "ExternalContentSources",
                columns: new[] { "Provider", "ExternalId", "ContentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalContentSources_ReviewedByUserId",
                schema: "petwork",
                table: "ExternalContentSources",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalContentSources_ReviewStatus_ImportedAt",
                schema: "petwork",
                table: "ExternalContentSources",
                columns: new[] { "ReviewStatus", "ImportedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentImportAudits",
                schema: "petwork");

            migrationBuilder.DropTable(
                name: "ExternalContentSources",
                schema: "petwork");
        }
    }
}
