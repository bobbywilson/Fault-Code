using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaultCode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowRecords",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ParentKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceClientId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    SourceEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRecords", x => new { x.TenantId, x.Kind, x.Key });
                    table.CheckConstraint("CK_WorkflowRecord_Json", "ISJSON([Payload]) = 1");
                    table.ForeignKey(
                        name: "FK_WorkflowRecords_Drivers_TenantId_DriverId",
                        columns: x => new { x.TenantId, x.DriverId },
                        principalTable: "Drivers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowRecords_Vehicles_TenantId_VehicleId",
                        columns: x => new { x.TenantId, x.VehicleId },
                        principalTable: "Vehicles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowRecords_WorkflowRecords_TenantId_ParentKind_ParentKey",
                        columns: x => new { x.TenantId, x.ParentKind, x.ParentKey },
                        principalTable: "WorkflowRecords",
                        principalColumns: new[] { "TenantId", "Kind", "Key" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecords_TenantId_DriverId",
                table: "WorkflowRecords",
                columns: new[] { "TenantId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecords_TenantId_Kind_Position",
                table: "WorkflowRecords",
                columns: new[] { "TenantId", "Kind", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecords_TenantId_ParentKind_ParentKey",
                table: "WorkflowRecords",
                columns: new[] { "TenantId", "ParentKind", "ParentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecords_TenantId_SourceClientId_SourceEventId",
                table: "WorkflowRecords",
                columns: new[] { "TenantId", "SourceClientId", "SourceEventId" },
                unique: true,
                filter: "[Kind] = 'incident' AND [SourceClientId] IS NOT NULL AND [SourceEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecords_TenantId_VehicleId",
                table: "WorkflowRecords",
                columns: new[] { "TenantId", "VehicleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowRecords");
        }
    }
}
