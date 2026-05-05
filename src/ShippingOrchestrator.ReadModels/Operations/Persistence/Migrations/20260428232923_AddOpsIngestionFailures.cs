using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpsIngestionFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_failures",
                schema: "ops_read",
                columns: table => new
                {
                    failure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    reason_code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    tenant_hint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dismissed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_failures", x => x.failure_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ops_ingestion_failures_status_reason_last",
                schema: "ops_read",
                table: "ingestion_failures",
                columns: new[] { "status", "reason_code", "last_occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ops_ingestion_failures_tenant_status",
                schema: "ops_read",
                table: "ingestion_failures",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_failures",
                schema: "ops_read");
        }
    }
}
