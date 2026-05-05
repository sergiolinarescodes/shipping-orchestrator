using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace ShippingOrchestrator.E2E.Tests.Infrastructure;

/// <summary>
/// Common base for E2E test fixtures. Truncates every test-managed schema before each
/// test runs so fixtures are independent — adding a second fixture cannot leak data into
/// the next via the shared composite host.
/// </summary>
public abstract class E2ETestBase
{
    [SetUp]
    public Task PerTestSetUp() => E2EFixture.Current.ResetAsync();

    /// <summary>
    /// Polls the customer dashboard's pending-orders endpoint until <paramref name="pendingOrderId"/>
    /// appears (or the timeout elapses). The dispatcher is fire-and-forget — webhook returns 202
    /// with a pre-allocated id before the handler has actually persisted the row — so any test
    /// that bundles or queries a pending order right after a webhook needs this gate, otherwise
    /// it races the in-flight Wolverine handler.
    /// </summary>
    protected static async Task WaitForPendingOrderAsync(
        Guid tenantId,
        Guid pendingOrderId,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(15));
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/dashboard/orders/pending?take=200");
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-Tenant-Role", "tenant");
            var resp = await E2EFixture.Current.HttpClient.SendAsync(request).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var rows = await resp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.GetProperty("id").GetGuid() == pendingOrderId) return;
                }
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
        Assert.Fail($"Pending order {pendingOrderId} for tenant {tenantId} did not become visible within the timeout.");
    }
}
