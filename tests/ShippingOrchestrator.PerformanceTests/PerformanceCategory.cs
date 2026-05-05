namespace ShippingOrchestrator.PerformanceTests;

/// <summary>
/// NUnit category tag for perf scenarios. Use it to filter (Rider's test explorer
/// "Group by Categories", or <c>dotnet test --filter Category=Performance</c>) when you
/// want to run just the perf set. Scenarios are otherwise discoverable like any other test.
/// </summary>
public static class PerformanceCategory
{
    public const string Name = "Performance";
}
