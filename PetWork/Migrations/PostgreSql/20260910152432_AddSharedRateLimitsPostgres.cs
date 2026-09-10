using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSharedRateLimitsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RateLimitUsages",
                schema: "petwork",
                columns: table => new
                {
                    Policy = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    SubjectHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WindowStartedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimitUsages", x => new { x.Policy, x.SubjectHash });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateLimitUsages",
                schema: "petwork");
        }
    }
}
