using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaultCode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyApiQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiRequestQuotas",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    IdentityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Used = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRequestQuotas", x => new { x.TenantId, x.IdentityHash, x.Day });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiRequestQuotas");
        }
    }
}
