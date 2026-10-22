using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaultCode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFleet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OwnerObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmailNotifications = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "FleetAudit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FleetReplays",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetReplays", x => new { x.TenantId, x.KeyHash });
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    System = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "DriverVehicleAssociations",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverVehicleAssociations", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_DriverVehicleAssociations_Drivers_TenantId_DriverId",
                        columns: x => new { x.TenantId, x.DriverId },
                        principalTable: "Drivers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverVehicleAssociations_Vehicles_TenantId_VehicleId",
                        columns: x => new { x.TenantId, x.VehicleId },
                        principalTable: "Vehicles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_Identifier",
                table: "Drivers",
                columns: new[] { "TenantId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_OwnerObjectId",
                table: "Drivers",
                columns: new[] { "TenantId", "OwnerObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleAssociations_TenantId_DriverId",
                table: "DriverVehicleAssociations",
                columns: new[] { "TenantId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleAssociations_TenantId_VehicleId_DriverId",
                table: "DriverVehicleAssociations",
                columns: new[] { "TenantId", "VehicleId", "DriverId" },
                unique: true,
                filter: "[Until] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FleetAudit_TenantId_Id",
                table: "FleetAudit",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_Identifier",
                table: "Vehicles",
                columns: new[] { "TenantId", "Identifier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverVehicleAssociations");

            migrationBuilder.DropTable(
                name: "FleetAudit");

            migrationBuilder.DropTable(
                name: "FleetReplays");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
