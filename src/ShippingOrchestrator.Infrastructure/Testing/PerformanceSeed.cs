using System.Globalization;
using System.Runtime.CompilerServices;
using Npgsql;

[assembly: InternalsVisibleTo("ShippingOrchestrator.PerformanceTests")]

namespace ShippingOrchestrator.Infrastructure.Testing;

/// <summary>
/// Bulk-seed helper used by the perf project so scenarios can stand up N tenants with one
/// ecommerce connection each without re-implementing tenant/connection/onboarding flows.
/// Bypasses the application layer on purpose — perf scenarios care about realistic data
/// shape, not the lifecycle invariants the command handlers enforce. <c>internal</c> +
/// <c>InternalsVisibleTo</c> keeps it out of production reach.
/// </summary>
internal static class PerformanceSeed
{
    /// <summary>
    /// Inserts <paramref name="tenantCount"/> tenants and one Shopify connection per tenant
    /// directly via raw SQL in a single transaction. Returns the inserted tenant ids so the
    /// scenario can drive traffic at a known set. The Shopify connection's external account
    /// id matches the pattern <c>perf-shop-{tenant:N}.myshopify.com</c> so the webhook
    /// scenario's <c>X-Shopify-Shop-Domain</c> header lookup hits the seeded row.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> SeedTenantsWithShopifyConnectionsAsync(
        string connectionString,
        int tenantCount,
        CancellationToken cancellationToken)
    {
        var tenantIds = new List<Guid>(tenantCount);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < tenantCount; i++)
        {
            var tenantId = Guid.NewGuid();
            tenantIds.Add(tenantId);

            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO orchestrator.tenants (id, display_name, status, carrier_mode, created_at, updated_at)
                  VALUES (@id, @name, 'Active', 'Master', now(), now())", conn, tx))
            {
                cmd.Parameters.AddWithValue("id", tenantId);
                cmd.Parameters.AddWithValue("name", "perf-tenant-" + i.ToString(CultureInfo.InvariantCulture));
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO orchestrator.ecommerce_connections
                  (id, tenant_id, platform_code, external_account_id, status, credentials_cipher, installed_at, verified_at)
                  VALUES (@id, @tenant, 'shopify', @account, 'Active', @creds, now(), now())", conn, tx))
            {
                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("tenant", tenantId);
                cmd.Parameters.AddWithValue("account", $"perf-shop-{tenantId:N}.myshopify.com");
                cmd.Parameters.AddWithValue("creds", new byte[] { 0x00 });
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return tenantIds;
    }

    /// <summary>
    /// Truncates the pending-orders write table and Wolverine's outbox/inbox so a scenario
    /// starts from a known queue/inbox state. Does NOT touch tenants — bulk-seed once per run,
    /// reset between scenarios.
    /// </summary>
    public static async Task ResetIngestionStateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            @"TRUNCATE orchestrator.pending_ecommerce_orders,
                       messaging.wolverine_outgoing_envelopes,
                       messaging.wolverine_incoming_envelopes", conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
