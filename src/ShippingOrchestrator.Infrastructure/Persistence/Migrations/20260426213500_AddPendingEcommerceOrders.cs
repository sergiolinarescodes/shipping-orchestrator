using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEcommerceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_ecommerce_orders",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_by_batch_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_ecommerce_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pending_ecommerce_orders_tenant_id_consumed_at",
                schema: "orchestrator",
                table: "pending_ecommerce_orders",
                columns: new[] { "tenant_id", "consumed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_pending_ecommerce_orders_tenant_id_platform_code_external_o~",
                schema: "orchestrator",
                table: "pending_ecommerce_orders",
                columns: new[] { "tenant_id", "platform_code", "external_order_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_ecommerce_orders",
                schema: "orchestrator");
        }
    }
}
