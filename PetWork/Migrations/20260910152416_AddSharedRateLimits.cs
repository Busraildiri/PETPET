using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedRateLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RateLimitUsages",
                columns: table => new
                {
                    Policy = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SubjectHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WindowStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "RateLimitUsages");
        }
    }
}
