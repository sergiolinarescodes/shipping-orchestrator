using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ShippingOrchestrator.PerformanceTests.Fixtures;

/// <summary>
/// Boots PublicApi in-process against the connection strings exposed by
/// <see cref="PerformanceStackFixture"/>. The factory overrides configuration with the
/// container endpoints + flips <c>Performance:ProbesEnabled</c> on so scenarios can read
/// queue depth and pending-order counts without scraping logs. Wolverine's auto-provision
/// runs against LocalStack on the dynamic port.
/// </summary>
public sealed class PerformanceWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PerformanceStackFixture _stack;

    public PerformanceWebApplicationFactory(PerformanceStackFixture stack)
    {
        _stack = stack;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting writes directly into IConfiguration via the chained-host settings, which
        // Program.cs picks up when it reads builder.Configuration.GetConnectionString(...)
        // during AddOrchestratorCore. AddInMemoryCollection from ConfigureAppConfiguration
        // would land in the source list before appsettings.json under WAF and lose the
        // precedence battle, leaving the in-process host pointed at the default 5432 db.
        builder.UseEnvironment("Development");
        // Append connection-pool tuning so the perf scenarios don't bottleneck on Npgsql's
        // default 100-conn ceiling at maxConcurrency: 96. Multiplexing lets a single physical
        // connection serve multiple commands concurrently — meaningful gain on read-heavy
        // hot paths (the dispatcher pre-check + handler's existing-row lookup). Production
        // sets the same in appsettings.json once tuned.
        var pgTuning = ";Maximum Pool Size=200;Multiplexing=true";
        builder.UseSetting("ConnectionStrings:Orchestrator", _stack.OrchestratorConnectionString + pgTuning);
        builder.UseSetting("ConnectionStrings:CustomerRead", _stack.OrchestratorConnectionString + pgTuning);
        builder.UseSetting("ConnectionStrings:Redis", _stack.RedisConnectionString);
        builder.UseSetting("Aws:LocalStackPort", _stack.LocalStackPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Messaging:AutoPurgeOnStartup", "true");
        builder.UseSetting("Performance:ProbesEnabled", "true");

        // Tame the Wolverine/EF/SignalR log stream so Rider's test output is dominated by
        // scenario results, not boot/teardown chatter. The two big offenders during teardown
        // are (a) Wolverine cancelling in-flight local-queue handlers when the host stops —
        // those land at Error — and (b) the SignalR Redis backplane logging the subscription
        // socket close as Error when the Redis container disposes. Neither is an actual test
        // failure, so squelch both at Critical.
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.UseSetting("Logging:LogLevel:Wolverine", "Critical");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.SignalR", "Critical");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.SignalR.StackExchangeRedis", "Critical");
        builder.UseSetting("Logging:LogLevel:ShippingOrchestrator", "Warning");

        // ASP.NET Core's default shutdown timeout is 5 seconds. Bump it so Wolverine's
        // ReleaseAllOwnership SQL command — issued during host StopAsync — has room to finish
        // when the connection pool is contested under saturation runs.
        builder.ConfigureServices(services => services.Configure<HostOptions>(o =>
        {
            o.ShutdownTimeout = TimeSpan.FromSeconds(30);
        }));
    }

    public override async ValueTask DisposeAsync()
    {
        // Even with the bumped ShutdownTimeout, Wolverine's per-store cleanup queries can race
        // the host's internal cancellation token under saturation. The query throws
        // TaskCanceledException out of host StopAsync → WAF DisposeAsync, which the test
        // runner reports as a failure even though the scenario assertions already passed.
        // Swallow the cancellation here so the dispose path doesn't drag a green run red.
        try
        {
            await base.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
