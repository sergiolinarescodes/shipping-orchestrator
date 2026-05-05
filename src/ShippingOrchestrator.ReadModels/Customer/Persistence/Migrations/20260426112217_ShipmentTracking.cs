using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Customer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShipmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipment_tracking_events",
                schema: "customer_read",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    event_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_tracking_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_tracking_events_shipment_id_sequence",
                schema: "customer_read",
                table: "shipment_tracking_events",
                columns: new[] { "shipment_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_tracking_events_tenant_id_occurred_at",
                schema: "customer_read",
                table: "shipment_tracking_events",
                columns: new[] { "tenant_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_tracking_events",
                schema: "customer_read");
        }
    }
}
