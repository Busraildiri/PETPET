using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddMobileSupportReportsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileSupportReports",
                schema: "petwork",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    ScreenshotPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileSupportReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileSupportReports_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "petwork",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileSupportReports_Status_CreatedAt",
                schema: "petwork",
                table: "MobileSupportReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileSupportReports_TrackingNumber",
                schema: "petwork",
                table: "MobileSupportReports",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileSupportReports_UserId",
                schema: "petwork",
                table: "MobileSupportReports",
                column: "UserId");

            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA petwork TO petwork_app;
                ALTER TABLE petwork."MobileSupportReports" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE petwork."MobileSupportReports" DISABLE ROW LEVEL SECURITY;
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE petwork."MobileSupportReports" TO petwork_app;
                GRANT USAGE, SELECT ON SEQUENCE petwork."MobileSupportReports_Id_seq" TO petwork_app;
                DROP POLICY IF EXISTS "MobileSupportReports_backend" ON petwork."MobileSupportReports";
                CREATE POLICY "MobileSupportReports_backend"
                    ON petwork."MobileSupportReports"
                    FOR ALL
                    TO petwork_app
                    USING (true)
                    WITH CHECK (true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileSupportReports",
                schema: "petwork");
        }
    }
}
