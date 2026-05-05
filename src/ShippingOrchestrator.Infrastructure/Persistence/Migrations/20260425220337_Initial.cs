using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "orchestrator");

            migrationBuilder.CreateTable(
                name: "carrier_assignments",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrier_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    origin_countries = table.Column<string[]>(type: "text[]", nullable: false),
                    destination_countries = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrier_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ecommerce_connections",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_account_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    credentials_cipher = table.Column<byte[]>(type: "bytea", nullable: false),
                    installed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sync_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecommerce_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_batches",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_lineage",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    rule_attribution = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_lineage", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    carrier_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tracking_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    label_uri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    from_address = table.Column<string>(type: "jsonb", nullable: false),
                    to_address = table.Column<string>(type: "jsonb", nullable: false),
                    parcel = table.Column<string>(type: "jsonb", nullable: false),
                    preferred_service = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_batch_items",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal_index = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_batch_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_batch_items_shipment_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "orchestrator",
                        principalTable: "shipment_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_carrier_assignments_tenant_id",
                schema: "orchestrator",
                table: "carrier_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_assignments_tenant_id_carrier_code",
                schema: "orchestrator",
                table: "carrier_assignments",
                columns: new[] { "tenant_id", "carrier_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ecommerce_connections_tenant_id",
                schema: "orchestrator",
                table: "ecommerce_connections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ecommerce_connections_tenant_id_platform_code_external_acco~",
                schema: "orchestrator",
                table: "ecommerce_connections",
                columns: new[] { "tenant_id", "platform_code", "external_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_batch_items_batch_id_ordinal_index",
                schema: "orchestrator",
                table: "shipment_batch_items",
                columns: new[] { "batch_id", "ordinal_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_batch_items_shipment_id",
                schema: "orchestrator",
                table: "shipment_batch_items",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_batches_tenant_id_idempotency_key",
                schema: "orchestrator",
                table: "shipment_batches",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_batches_tenant_id_status",
                schema: "orchestrator",
                table: "shipment_batches",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_lineage_shipment_id",
                schema: "orchestrator",
                table: "shipment_lineage",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_batch_id",
                schema: "orchestrator",
                table: "shipments",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_status",
                schema: "orchestrator",
                table: "shipments",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carrier_assignments",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "ecommerce_connections",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "shipment_batch_items",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "shipment_lineage",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "shipment_batches",
                schema: "orchestrator");
        }
    }
}
