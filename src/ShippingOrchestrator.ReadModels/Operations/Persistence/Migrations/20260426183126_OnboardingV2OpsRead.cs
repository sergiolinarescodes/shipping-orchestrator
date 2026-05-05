using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.ReadModels.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingV2OpsRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_type",
                schema: "ops_read",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_type",
                schema: "ops_read",
                table: "tenants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
