using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Onboarding;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.UnitTests.Onboarding;

[TestFixture]
public class OnboardingProcessTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static OnboardingFlowBlueprint Blueprint() => new(
        FlowCode: "manual-staff-v1",
        Steps:
        [
            new("tenant.create", 1, false, IsCommitted: true),
            new("connection.shopify.start", 2, false, IsCommitted: false),
            new("connection.shopify.complete", 3, false, IsCommitted: true),
            new("carrier.assign.postnl", 4, false, IsCommitted: true),
            new("tenant.activate", 5, false, IsCommitted: true),
        ]);

    [Test]
    public void Start_materialises_all_steps_in_order()
    {
        var p = OnboardingProcess.Start(Blueprint(), startedByStaffUserId: "ops", contactEmail: "ops@acme.test", Now);

        p.Status.Should().Be(OnboardingProcessStatus.InProgress);
        p.FlowCode.Should().Be("manual-staff-v1");
        p.Steps.Should().HaveCount(5);
        p.Steps.Select(s => s.Code).Should().Equal(
            "tenant.create", "connection.shopify.start", "connection.shopify.complete",
            "carrier.assign.postnl", "tenant.activate");
        p.Steps.All(s => s.Status == OnboardingStepStatus.Pending).Should().BeTrue();
    }

    [Test]
    public void CompleteStep_out_of_order_throws()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        Action act = () => p.CompleteStep("carrier.assign.postnl", null, null, null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void CompleteStep_is_idempotent()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        var tenantId = TenantId.New();
        var result = JsonSerializer.SerializeToDocument(new { tenantId = tenantId.Value });

        p.CompleteStep("tenant.create", null, result, tenantId, Now);
        p.CompleteStep("tenant.create", null, result, tenantId, Now);

        var step = p.Steps.Single(s => s.Code == "tenant.create");
        step.Status.Should().Be(OnboardingStepStatus.Completed);
        p.TenantId.Should().Be(tenantId);
    }

    [Test]
    public void Completing_all_steps_completes_the_process()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        var tenantId = TenantId.New();
        p.CompleteStep("tenant.create", null, null, tenantId, Now);
        p.MarkStepAwaiting("connection.shopify.start", "corr-1", null, Now);
        p.CompleteStep("connection.shopify.start", null, null, null, Now);
        p.CompleteStep("connection.shopify.complete", null, null, null, Now);
        p.CompleteStep("carrier.assign.postnl", null, null, null, Now);
        p.CompleteStep("tenant.activate", null, null, null, Now);

        p.Status.Should().Be(OnboardingProcessStatus.Completed);
        p.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void Rewind_blocked_past_committed_step()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        var tenantId = TenantId.New();
        p.CompleteStep("tenant.create", null, null, tenantId, Now);

        var blockingCode = p.RewindTo("tenant.create", Now);
        blockingCode.Should().Be("tenant.create"); // tenant.create itself is committed; can't rewind into it
    }

    [Test]
    public void Rewind_resets_subsequent_pending_steps()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        var tenantId = TenantId.New();
        p.CompleteStep("tenant.create", null, null, tenantId, Now);
        p.MarkStepAwaiting("connection.shopify.start", "corr-1", null, Now);

        var blocker = p.RewindTo("connection.shopify.start", Now);
        blocker.Should().BeNull();
        p.Steps.Single(s => s.Code == "connection.shopify.start").Status.Should().Be(OnboardingStepStatus.Pending);
    }

    [Test]
    public void TimeOut_only_when_step_is_awaiting()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        p.CompleteStep("tenant.create", null, null, TenantId.New(), Now);
        p.MarkStepAwaiting("connection.shopify.start", "corr-1", DateTimeOffset.UtcNow.AddMinutes(15), Now);

        p.TimeOut("connection.shopify.start", Now.AddMinutes(20));

        p.Status.Should().Be(OnboardingProcessStatus.TimedOut);
        p.Steps.Single(s => s.Code == "connection.shopify.start").Status.Should().Be(OnboardingStepStatus.Failed);
    }

    [Test]
    public void Cancel_terminates_in_progress_process()
    {
        var p = OnboardingProcess.Start(Blueprint(), null, null, Now);
        p.Cancel("operator-cancel", Now);
        p.Status.Should().Be(OnboardingProcessStatus.Cancelled);
        p.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void Empty_blueprint_throws()
    {
        var empty = new OnboardingFlowBlueprint("test", []);
        Action act = () => OnboardingProcess.Start(empty, null, null, Now);
        act.Should().Throw<ArgumentException>();
    }
}
