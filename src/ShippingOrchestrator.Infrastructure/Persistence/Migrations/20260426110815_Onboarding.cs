using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Onboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_processes",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_by_staff_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_processes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_steps",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    collected_payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    result_payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    external_correlation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    awaiting_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_onboarding_steps_onboarding_processes_process_id",
                        column: x => x.process_id,
                        principalSchema: "orchestrator",
                        principalTable: "onboarding_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_processes_flow_code_status",
                schema: "orchestrator",
                table: "onboarding_processes",
                columns: new[] { "flow_code", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_processes_tenant_id",
                schema: "orchestrator",
                table: "onboarding_processes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_steps_external_correlation_id",
                schema: "orchestrator",
                table: "onboarding_steps",
                column: "external_correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_steps_process_id_code",
                schema: "orchestrator",
                table: "onboarding_steps",
                columns: new[] { "process_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_steps",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "onboarding_processes",
                schema: "orchestrator");
        }
    }
}
