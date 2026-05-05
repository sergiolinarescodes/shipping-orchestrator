using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EcommerceConnectionDisconnectColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "disconnect_reason",
                schema: "orchestrator",
                table: "ecommerce_connections",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disconnected_at",
                schema: "orchestrator",
                table: "ecommerce_connections",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disconnect_reason",
                schema: "orchestrator",
                table: "ecommerce_connections");

            migrationBuilder.DropColumn(
                name: "disconnected_at",
                schema: "orchestrator",
                table: "ecommerce_connections");
        }
    }
}
