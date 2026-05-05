using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_sessions_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "orchestrator",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_invitations",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    invited_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_invitations_accounts_invited_by_account_id",
                        column: x => x.invited_by_account_id,
                        principalSchema: "orchestrator",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_invitations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "orchestrator",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                schema: "orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    granted_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_memberships_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "orchestrator",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "orchestrator",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_email",
                schema: "orchestrator",
                table: "accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_account_id",
                schema: "orchestrator",
                table: "auth_sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_session_hash",
                schema: "orchestrator",
                table: "auth_sessions",
                column: "session_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_email_expires_at",
                schema: "orchestrator",
                table: "magic_link_tokens",
                columns: new[] { "email", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_token_hash",
                schema: "orchestrator",
                table: "magic_link_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invitations_email_tenant_id",
                schema: "orchestrator",
                table: "tenant_invitations",
                columns: new[] { "email", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invitations_invited_by_account_id",
                schema: "orchestrator",
                table: "tenant_invitations",
                column: "invited_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invitations_tenant_id",
                schema: "orchestrator",
                table: "tenant_invitations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_account_id_tenant_id",
                schema: "orchestrator",
                table: "tenant_memberships",
                columns: new[] { "account_id", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_tenant_id",
                schema: "orchestrator",
                table: "tenant_memberships",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "magic_link_tokens",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "tenant_invitations",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "orchestrator");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "orchestrator");
        }
    }
}
