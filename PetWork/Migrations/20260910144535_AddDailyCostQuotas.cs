using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCostQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCostUsages",
                columns: table => new
                {
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubjectHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UsageDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCostUsages", x => new { x.Category, x.SubjectHash, x.UsageDateUtc });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DailyCostUsages");
        }
    }
}
