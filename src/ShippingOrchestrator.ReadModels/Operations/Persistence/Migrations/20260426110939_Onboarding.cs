using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Onboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_processes",
                schema: "ops_read",
                columns: table => new
                {
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_by_staff_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    current_step_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_processes", x => x.process_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_processes_flow_code_status",
                schema: "ops_read",
                table: "onboarding_processes",
                columns: new[] { "flow_code", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_processes_tenant_id",
                schema: "ops_read",
                table: "onboarding_processes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_processes",
                schema: "ops_read");
        }
    }
}
