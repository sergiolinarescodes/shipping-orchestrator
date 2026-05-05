using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpsReadHotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_ops_batches_status_created_desc",
                schema: "ops_read",
                table: "batches",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ops_shipments_exceptions_updated",
                schema: "ops_read",
                table: "shipments",
                column: "updated_at",
                filter: "\"status\" = 'Failed' OR \"failure_reason\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ops_ingestion_failures_last",
                schema: "ops_read",
                table: "ingestion_failures",
                column: "last_occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_ops_ingestion_failures_tenant_last_desc",
                schema: "ops_read",
                table: "ingestion_failures",
                columns: new[] { "tenant_id", "last_occurred_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ops_batches_status_created_desc",
                schema: "ops_read",
                table: "batches");

            migrationBuilder.DropIndex(
                name: "ix_ops_shipments_exceptions_updated",
                schema: "ops_read",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "ix_ops_ingestion_failures_last",
                schema: "ops_read",
                table: "ingestion_failures");

            migrationBuilder.DropIndex(
                name: "ix_ops_ingestion_failures_tenant_last_desc",
                schema: "ops_read",
                table: "ingestion_failures");
        }
    }
}
