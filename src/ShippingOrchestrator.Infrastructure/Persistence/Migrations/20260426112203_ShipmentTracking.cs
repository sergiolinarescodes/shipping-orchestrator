using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShipmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipment_tracking_events",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    event_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_tracking_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_tracking_events_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "orchestrator",
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_tracking_events_occurred_at",
                schema: "orchestrator",
                table: "shipment_tracking_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_tracking_events_shipment_id_sequence",
                schema: "orchestrator",
                table: "shipment_tracking_events",
                columns: new[] { "shipment_id", "sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_tracking_events",
                schema: "orchestrator");
        }
    }
}
