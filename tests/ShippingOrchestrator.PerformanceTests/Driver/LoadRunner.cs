using System.Collections.Concurrent;
using System.Diagnostics;

namespace ShippingOrchestrator.PerformanceTests.Driver;

/// <summary>
/// Minimal in-tree load driver — Stopwatch + ConcurrentBag of latencies. Replaces NBomber for
/// this project because NBomber 6.1.0's reporter pulls Spectre.Console &lt;=0.45.0 while
/// WolverineFx pulls JasperFx 1.26 → Spectre.Console &gt;=0.55.0, and there's no pin that
/// satisfies both. Two run modes:
/// <list type="bullet">
///   <item><b>Rate-paced</b> — fire at a fixed RPS for a fixed duration. Measures behaviour at
///   a target load (fairness, rate-limiter caps, controlled spikes).</item>
///   <item><b>Saturation</b> — fire as fast as <c>maxConcurrency</c> allows until
///   <c>targetCount</c> requests have been issued. Measures the architectural ceiling — what
///   RPS the system can actually sustain on this machine.</item>
/// </list>
/// Reports are written as a Markdown file under <c>bin/.../reports</c>.
/// </summary>
public sealed class LoadRunner
{
    private readonly Func<int, CancellationToken, Task<bool>> _step;
    private readonly int? _ratePerSecond;
    private readonly TimeSpan? _duration;
    private readonly int? _targetCount;
    private readonly int _maxConcurrency;

    /// <summary>Rate-paced run: fire at <paramref name="ratePerSecond"/> for <paramref name="duration"/>.</summary>
    public LoadRunner(
        Func<int, CancellationToken, Task<bool>> step,
        int ratePerSecond,
        TimeSpan duration,
        int maxConcurrency = 256)
    {
        _step = step;
        _ratePerSecond = ratePerSecond;
        _duration = duration;
        _targetCount = null;
        _maxConcurrency = maxConcurrency;
    }

    private LoadRunner(
        Func<int, CancellationToken, Task<bool>> step,
        int targetCount,
        int maxConcurrency)
    {
        _step = step;
        _ratePerSecond = null;
        _duration = null;
        _targetCount = targetCount;
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>Saturation run: dispatch as fast as <paramref name="maxConcurrency"/> allows
    /// until <paramref name="targetCount"/> requests have been issued. Achieved RPS is the
    /// architectural ceiling for the per-call work the step performs.</summary>
    public static LoadRunner Saturation(
        Func<int, CancellationToken, Task<bool>> step,
        int targetCount,
        int maxConcurrency = 256)
        => new(step, targetCount, maxConcurrency);

    public async Task<LoadResult> RunAsync(string scenarioName, CancellationToken cancellationToken = default)
    {
        var latencies = new ConcurrentBag<double>();
        var ok = 0;
        var fail = 0;
        var stopwatch = Stopwatch.StartNew();
        using var inflight = new SemaphoreSlim(_maxConcurrency);

        var requestIndex = 0;
        var capacityHint = _targetCount
            ?? ((_ratePerSecond ?? 0) * (int)(_duration?.TotalSeconds ?? 0));
        var pending = new List<Task>(capacity: capacityHint + 16);

        if (_targetCount is not null)
        {
            // Saturation: dispatch up to maxConcurrency in parallel until targetCount requests
            // have been issued. The semaphore throttles. No Task.Delay — we want the pool to
            // saturate.
            var target = _targetCount.Value;
            while (Volatile.Read(ref requestIndex) < target && !cancellationToken.IsCancellationRequested)
            {
                await inflight.WaitAsync(cancellationToken).ConfigureAwait(false);
                var idx = Interlocked.Increment(ref requestIndex);
                if (idx > target)
                {
                    inflight.Release();
                    break;
                }
                pending.Add(RunOne(idx, latencies, inflight, cancellationToken));
            }
        }
        else
        {
            // Rate-paced: fire at fixed cadence for the configured duration.
            var deadline = stopwatch.Elapsed + _duration!.Value;
            var interval = TimeSpan.FromSeconds(1.0 / _ratePerSecond!.Value);
            var nextDispatch = stopwatch.Elapsed;

            while (stopwatch.Elapsed < deadline && !cancellationToken.IsCancellationRequested)
            {
                var sleep = nextDispatch - stopwatch.Elapsed;
                if (sleep > TimeSpan.Zero) await Task.Delay(sleep, cancellationToken).ConfigureAwait(false);

                await inflight.WaitAsync(cancellationToken).ConfigureAwait(false);
                var idx = Interlocked.Increment(ref requestIndex);
                pending.Add(RunOne(idx, latencies, inflight, cancellationToken));
                nextDispatch += interval;
            }
        }

        await Task.WhenAll(pending).ConfigureAwait(false);
        stopwatch.Stop();

        // Drain window: when bus.SendAsync routes to a local Wolverine queue (the dispatcher's
        // path when a handler is in-process), the issuing call returns before the handler
        // commits to Postgres. Without this pause the WAF's dispose at scope-exit cancels
        // in-flight handlers and Wolverine logs them as Error — alarming-looking but benign.
        // Saturation runs with maxConcurrency=128 can leave hundreds queued; 3s covers them
        // on a laptop. Bigger backlogs surface as the OK count not matching the issued count.
        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None).ConfigureAwait(false);

        var sorted = latencies.OrderBy(l => l).ToArray();
        return new LoadResult(
            scenarioName,
            stopwatch.Elapsed,
            sorted.Length,
            ok,
            fail,
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            Percentile(sorted, 0.99),
            sorted.Length > 0 ? sorted[^1] : 0.0,
            sorted.Length > 0 ? sorted.Average() : 0.0);

        Task RunOne(int idx, ConcurrentBag<double> bag, SemaphoreSlim sem, CancellationToken ct)
            => Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var success = await _step(idx, ct).ConfigureAwait(false);
                    sw.Stop();
                    bag.Add(sw.Elapsed.TotalMilliseconds);
                    if (success) Interlocked.Increment(ref ok);
                    else Interlocked.Increment(ref fail);
                }
                catch
                {
                    sw.Stop();
                    Interlocked.Increment(ref fail);
                }
                finally
                {
                    sem.Release();
                }
            }, ct);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0.0;
        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}

public sealed record LoadResult(
    string ScenarioName,
    TimeSpan Duration,
    int Total,
    int OkCount,
    int FailCount,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs,
    double MeanMs)
{
    public double EffectiveRps => Total / Math.Max(0.001, Duration.TotalSeconds);

    public string ToMarkdown() => $"""
        # {ScenarioName}

        - **Duration:** {Duration.TotalSeconds:F1}s
        - **Total:** {Total}  •  **OK:** {OkCount}  •  **Fail:** {FailCount}
        - **Latency (ms):**  mean {MeanMs:F1}  •  p50 {P50Ms:F1}  •  p95 {P95Ms:F1}  •  p99 {P99Ms:F1}  •  max {MaxMs:F1}
        - **Effective RPS:** {EffectiveRps:F1}
        """;

    public void WriteReport(string scenarioFolder)
    {
        Directory.CreateDirectory(scenarioFolder);
        var reportPath = Path.Combine(scenarioFolder, "report.md");
        File.WriteAllText(reportPath, ToMarkdown());

        // Frame the summary with hard rules so it survives any noise above/below it in the
        // Rider test output panel. Both Progress (live tail) and Out (final test output)
        // get the lines so the result shows up regardless of how the runner buffers.
        var lines = new[]
        {
            "",
            "============================================================",
            $"  PERF · {ScenarioName}",
            "------------------------------------------------------------",
            $"  Duration       : {Duration.TotalSeconds,8:F1}s     Total    : {Total,8}",
            $"  OK / Fail      : {OkCount,8} / {FailCount,-8}  Effective: {EffectiveRps,7:F1} rps",
            $"  Latency (ms)   : mean {MeanMs,7:F1}  p50 {P50Ms,7:F1}  p95 {P95Ms,7:F1}",
            $"                   p99  {P99Ms,7:F1}  max {MaxMs,7:F1}",
            $"  Report         : {reportPath}",
            "============================================================",
            "",
        };
        foreach (var line in lines) Emit(line);
    }

    private static void Emit(string message)
    {
        try
        {
            NUnit.Framework.TestContext.Progress.WriteLine(message);
            NUnit.Framework.TestContext.Out.WriteLine(message);
        }
        catch
        {
            Console.WriteLine(message);
        }
    }
}
