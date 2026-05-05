using FluentAssertions;
using NUnit.Framework;

namespace ShippingOrchestrator.Architecture.Tests;

/// <summary>
/// Guards the discipline that projection handlers commit their cross-schema saves
/// sequentially (ops first, customer second). Parallel <c>Task.WhenAll(opsSave, customerSave)</c>
/// can leave the two read schemas drifted on a partial failure between the two completions.
/// Wolverine retries the whole handler on failure and the upserts are PK-keyed → sequential
/// commits converge on redelivery.
/// </summary>
[TestFixture]
public class ProjectionCommitOrderTests
{
    [Test]
    public void Projection_handlers_do_not_use_Task_WhenAll_for_SaveChangesAsync()
    {
        var projectionsDir = LocateProjectionsDirectory();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(projectionsDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            // Catches `Task.WhenAll(<anything>SaveChangesAsync<anything>)` regardless of arg order.
            if (text.Contains("Task.WhenAll(", StringComparison.Ordinal)
                && text.Contains("SaveChangesAsync", StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.IsMatch(
                    text, @"Task\.WhenAll\([^)]*SaveChangesAsync"))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        offenders.Should().BeEmpty(
            "projection handlers must commit ops + customer sequentially. Offenders: {0}",
            string.Join(", ", offenders));
    }

    private static string LocateProjectionsDirectory()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ShippingOrchestrator.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("repo root with ShippingOrchestrator.slnx must be locatable from the test directory");
        var path = Path.Combine(dir!.FullName, "src", "ShippingOrchestrator.ReadModels", "Projections");
        Directory.Exists(path).Should().BeTrue("Projections folder should exist at {0}", path);
        return path;
    }
}
