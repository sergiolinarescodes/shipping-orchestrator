using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_failures",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    lookup_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    tenant_hint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    raw_body_excerpt = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    raw_body_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    context_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dismissed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_failures", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_failures_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "orchestrator",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_failures_occurred_at",
                schema: "orchestrator",
                table: "ingestion_failures",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_failures_tenant_id_status",
                schema: "orchestrator",
                table: "ingestion_failures",
                columns: new[] { "tenant_id", "status" });

            // Partial unique index: one Open row per (tenant, connector, lookup_key). The lookup
            // key is normally external_order_id, falling back to "hash:" + raw_body_hash when the
            // body was unparseable. Resolved/Dismissed rows fall outside the filter so a fresh
            // failure on the same order can spawn a new Open row after a previous resolve.
            migrationBuilder.CreateIndex(
                name: "ix_ingestion_failures_open_unique",
                schema: "orchestrator",
                table: "ingestion_failures",
                columns: new[] { "tenant_id", "connector_code", "lookup_key" },
                unique: true,
                filter: "status = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_failures",
                schema: "orchestrator");
        }
    }
}
