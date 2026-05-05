using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops_read");

            migrationBuilder.CreateTable(
                name: "batches",
                schema: "ops_read",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    parcel_count = table.Column<int>(type: "integer", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.batch_id);
                });

            migrationBuilder.CreateTable(
                name: "carrier_daily_kpis",
                schema: "ops_read",
                columns: table => new
                {
                    carrier_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    total_label_duration_ms = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrier_daily_kpis", x => new { x.carrier_code, x.date });
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "ops_read",
                columns: table => new
                {
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    carrier_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tracking_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    country_from = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_to = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.shipment_id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "ops_read",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.tenant_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_batches_created_at",
                schema: "ops_read",
                table: "batches",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_batches_status",
                schema: "ops_read",
                table: "batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_carrier_code",
                schema: "ops_read",
                table: "shipments",
                column: "carrier_code");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_status",
                schema: "ops_read",
                table: "shipments",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batches",
                schema: "ops_read");

            migrationBuilder.DropTable(
                name: "carrier_daily_kpis",
                schema: "ops_read");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "ops_read");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "ops_read");
        }
    }
}
