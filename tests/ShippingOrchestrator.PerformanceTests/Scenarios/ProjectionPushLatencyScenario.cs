using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using NUnit.Framework;
using ShippingOrchestrator.PerformanceTests.Fixtures;
using ShippingOrchestrator.PublicApi.Realtime;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Measures end-to-end latency of the realtime push leg: connect a SignalR client to the
/// in-process hub, fire <see cref="IRealtimeNotifier.NotifyDashboardAsync"/> N times, and
/// record how long each event takes to land at the client. Tests the
/// PublicApi → SignalR → client path without booting Worker. Test cases scale the
/// total event count.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class ProjectionPushLatencyScenario
{

    [TestCase(10_000)]

    public async Task Realtime_push_holds_latency_for_target_event_count(int totalEvents)
    {
        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var factory = new PerformanceWebApplicationFactory(stack);
        var server = factory.Server;
        // Force the host to fully boot before the hub URL is reachable.
        using var _ = factory.CreateClient();

        var tenantId = Guid.NewGuid();
        await SeedSingleTenantAsync(stack, tenantId);

        var receivedAt = new ConcurrentBag<long>();
        var sentTimestamps = new ConcurrentDictionary<int, long>();
        var sw = Stopwatch.StartNew();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/v1/realtime"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.WebSocketFactory = (_, _) => throw new NotSupportedException();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        hubConnection.On<object>("dashboard:invalidate", _ =>
        {
            receivedAt.Add(sw.ElapsedTicks);
        });

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("SubscribeTenant", tenantId);

        var notifier = (IRealtimeNotifier)factory.Services.GetService(typeof(IRealtimeNotifier))!;

        // Fire N events with controlled pacing so the receive side has time to drain.
        const int ratePerSecond = 500;
        var interval = TimeSpan.FromSeconds(1.0 / ratePerSecond);
        for (var i = 0; i < totalEvents; i++)
        {
            sentTimestamps[i] = sw.ElapsedTicks;
            await notifier.NotifyDashboardAsync(tenantId, "dashboard:invalidate", new { area = "orders", seq = i });
            if ((i % ratePerSecond) == 0) await Task.Delay(interval);
        }

        // Wait briefly for the receiver to drain.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, totalEvents / ratePerSecond + 5));
        while (receivedAt.Count < totalEvents && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        await hubConnection.StopAsync();
        sw.Stop();

        var receivedCount = receivedAt.Count;
        var deliveryRate = receivedCount / (double)totalEvents;
        Assert.That(deliveryRate, Is.GreaterThan(0.95),
            $"Expected ≥95% of {totalEvents} events delivered to the SignalR client (saw {receivedCount}).");

        TestContext.Progress.WriteLine(
            $"[realtime-push] sent={totalEvents} received={receivedCount} delivery={deliveryRate:P1}");
    }

    private static async Task SeedSingleTenantAsync(PerformanceStackFixture stack, Guid tenantId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(stack.OrchestratorConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(
            @"INSERT INTO orchestrator.tenants (id, display_name, status, carrier_mode, created_at, updated_at)
              VALUES (@id, 'realtime-tenant', 'Active', 'Master', now(), now())", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }
}
