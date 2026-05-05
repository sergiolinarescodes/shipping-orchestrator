using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Customer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerReadHotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_customer_shipments_batch_created",
                schema: "customer_read",
                table: "shipments",
                columns: new[] { "batch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_ingestion_failures_tenant_status_last_desc",
                schema: "customer_read",
                table: "ingestion_failures",
                columns: new[] { "tenant_id", "status", "last_occurred_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_customer_batches_tenant_status_created_desc",
                schema: "customer_read",
                table: "batches",
                columns: new[] { "tenant_id", "status", "created_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customer_shipments_batch_created",
                schema: "customer_read",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "ix_customer_ingestion_failures_tenant_status_last_desc",
                schema: "customer_read",
                table: "ingestion_failures");

            migrationBuilder.DropIndex(
                name: "ix_customer_batches_tenant_status_created_desc",
                schema: "customer_read",
                table: "batches");
        }
    }
}
