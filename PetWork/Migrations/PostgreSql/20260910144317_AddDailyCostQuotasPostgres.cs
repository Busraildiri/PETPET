using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddDailyCostQuotasPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCostUsages",
                schema: "petwork",
                columns: table => new
                {
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubjectHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsageDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCostUsages", x => new { x.Category, x.SubjectHash, x.UsageDateUtc });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCostUsages",
                schema: "petwork");
        }
    }
}
