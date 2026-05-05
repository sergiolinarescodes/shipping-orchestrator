using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_type",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.AddColumn<string>(
                name: "carrier_mode",
                schema: "orchestrator",
                table: "tenants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "tos_accepted_at",
                schema: "orchestrator",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tos_ip",
                schema: "orchestrator",
                table: "tenants",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tos_signer_email",
                schema: "orchestrator",
                table: "tenants",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tos_signer_name",
                schema: "orchestrator",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tos_version",
                schema: "orchestrator",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at",
                schema: "orchestrator",
                table: "ecommerce_connections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "orchestrator",
                table: "ecommerce_connections",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "orchestrator",
                table: "ecommerce_connections",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verified_at",
                schema: "orchestrator",
                table: "ecommerce_connections",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carrier_mode",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tos_accepted_at",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tos_ip",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tos_signer_email",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tos_signer_name",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tos_version",
                schema: "orchestrator",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                schema: "orchestrator",
                table: "ecommerce_connections");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "orchestrator",
                table: "ecommerce_connections");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "orchestrator",
                table: "ecommerce_connections");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "orchestrator",
                table: "ecommerce_connections");

            migrationBuilder.AddColumn<string>(
                name: "client_type",
                schema: "orchestrator",
                table: "tenants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
