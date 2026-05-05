using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-flight: any existing rows pointing at a non-existent tenant would block the
            // FK's CHECK on apply. Surface and remove them in a deterministic order so the
            // migration is idempotent — environments without orphans are no-ops, environments
            // with orphans (typical of pre-FK dev stacks where TestTenantAuthHandler accepted
            // any header) get cleaned up before the constraint is added. Order matters because
            // shipments/items/lineage/tracking are children of batches via existing FKs.
            migrationBuilder.Sql(@"
                DELETE FROM orchestrator.shipment_tracking_events
                 WHERE shipment_id IN (
                    SELECT s.id FROM orchestrator.shipments s
                    LEFT JOIN orchestrator.tenants t ON t.id = s.tenant_id
                     WHERE t.id IS NULL);

                DELETE FROM orchestrator.shipment_lineage
                 WHERE shipment_id IN (
                    SELECT s.id FROM orchestrator.shipments s
                    LEFT JOIN orchestrator.tenants t ON t.id = s.tenant_id
                     WHERE t.id IS NULL);

                DELETE FROM orchestrator.shipment_batch_items
                 WHERE batch_id IN (
                    SELECT b.id FROM orchestrator.shipment_batches b
                    LEFT JOIN orchestrator.tenants t ON t.id = b.tenant_id
                     WHERE t.id IS NULL);

                DELETE FROM orchestrator.shipments
                 WHERE batch_id IN (
                    SELECT b.id FROM orchestrator.shipment_batches b
                    LEFT JOIN orchestrator.tenants t ON t.id = b.tenant_id
                     WHERE t.id IS NULL);

                DELETE FROM orchestrator.shipment_batches
                 WHERE tenant_id NOT IN (SELECT id FROM orchestrator.tenants);

                DELETE FROM orchestrator.ecommerce_connections
                 WHERE tenant_id NOT IN (SELECT id FROM orchestrator.tenants);

                DELETE FROM orchestrator.pending_ecommerce_orders
                 WHERE tenant_id NOT IN (SELECT id FROM orchestrator.tenants);
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_ecommerce_connections_tenants_tenant_id",
                schema: "orchestrator",
                table: "ecommerce_connections",
                column: "tenant_id",
                principalSchema: "orchestrator",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_batches_tenants_tenant_id",
                schema: "orchestrator",
                table: "shipment_batches",
                column: "tenant_id",
                principalSchema: "orchestrator",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ecommerce_connections_tenants_tenant_id",
                schema: "orchestrator",
                table: "ecommerce_connections");

            migrationBuilder.DropForeignKey(
                name: "FK_shipment_batches_tenants_tenant_id",
                schema: "orchestrator",
                table: "shipment_batches");
        }
    }
}
