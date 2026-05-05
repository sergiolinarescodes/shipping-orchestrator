using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Application.Shipments;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.PrivateApi.Endpoints;
using ShippingOrchestrator.PublicApi.Endpoints;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;

namespace ShippingOrchestrator.E2E.Tests;

[TestFixture]
public class HappyPathTests : E2ETestBase
{
    [Test]
    public async Task Onboard_tenant_assign_postnl_post_batch_wait_for_completion()
    {
        var tenantResponse = await StaffPost("/admin/tenants/",
            new CreateTenantHttpRequest("Acme NL", "ops@acme.test"));
        tenantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        tenant.Should().NotBeNull();
        var tenantId = tenant.TenantId;

        (await StaffPost($"/admin/tenants/{tenantId}/activate", body: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var carrierResponse = await StaffPost(
            $"/admin/tenants/{tenantId}/carrier-assignments",
            new CreateCarrierAssignmentHttpRequest(
                CarrierCode: "postnl",
                Priority: 100,
                OriginCountries: new[] { "NL", "BE" },
                DestinationCountries: new[] { "*" }));
        carrierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var assignment = await carrierResponse.Content.ReadFromJsonAsync<CreateCarrierAssignmentResponse>();
        assignment!.AssignmentId.Should().NotBeEmpty();

        var installResponse = await TenantPost(tenantId, "/v1/connections/shopify/install",
            new StartOAuthHttpRequest(
                ExternalAccountId: "acme-nl.myshopify.com",
                RedirectUri: "https://app.local/callback",
                State: "abc123"));
        installResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var install = await installResponse.Content.ReadFromJsonAsync<StartConnectionInstallResponse>();
        install!.AuthorizationUrl.Should().Contain("/admin/oauth/authorize");
        install.AuthorizationUrl.Should().Contain("state=abc123");

        var batchRequest = new CreateBatchHttpRequest(new[]
        {
            ShipmentFor(from: "NL", to: "BE"),
            ShipmentFor(from: "NL", to: "DE"),
            ShipmentFor(from: "NL", to: "FR"),
        });
        var batchResponse = await TenantPost(tenantId, "/v1/shipments/batches", batchRequest);
        batchResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var batchAccepted = await batchResponse.Content.ReadFromJsonAsync<CreateShipmentBatchResponse>();
        batchAccepted!.ShipmentIds.Should().HaveCount(3);
        var batchId = batchAccepted.BatchId;

        var completion = await E2EFixture.Current.BatchSignal
            .WaitAsync(batchId, TimeSpan.FromSeconds(30));
        completion.SuccessCount.Should().Be(3);
        completion.FailureCount.Should().Be(0);

        // After labelling, the carrier tracking poll promotes shipments past Labeled into
        // InTransit / Delivered as soon as events arrive. Accept any post-label status here.
        var labeledOrLater = new[] { "Labeled", "InTransit", "Delivered" };
        var batchView = await PollUntilReadModelHasBatch(tenantId, batchId,
            expectedBatchStatus: "Completed",
            isShipmentSettled: s => labeledOrLater.Contains(s.Status));
        batchView.SuccessCount.Should().Be(3);
        batchView.FailureCount.Should().Be(0);
        batchView.Shipments.Should().HaveCount(3);
        foreach (var s in batchView.Shipments)
        {
            s.Status.Should().BeOneOf(labeledOrLater);
            s.CarrierCode.Should().Be("postnl");
            s.TrackingNumber.Should().NotBeNullOrEmpty();
            s.LabelUri.Should().StartWith("https://mock.postnl.local/labels/");
        }

        var dashboardResponse = await TenantGet(tenantId, "/v1/dashboard/shipments");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<List<CustomerShipmentView>>();
        dashboard!.Should().HaveCount(3);

        var opsQueuesResponse = await StaffGet("/ops/queues?status=Completed");
        opsQueuesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var opsQueues = await opsQueuesResponse.Content.ReadFromJsonAsync<List<OpsBatchRow>>();
        opsQueues!.Should().Contain(r => r.BatchId == batchId);

        var kpiResponse = await StaffGet("/ops/kpis/carrier-success-rate");
        kpiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var kpis = await kpiResponse.Content.ReadFromJsonAsync<List<OpsCarrierKpi>>();
        kpis.Should().NotBeNull();
    }

    private static async Task<CustomerBatchView> PollUntilReadModelHasBatch(
        Guid tenantId,
        Guid batchId,
        string expectedBatchStatus,
        Func<CustomerShipmentView, bool> isShipmentSettled)
    {
        // Read-side projections run as separate Wolverine handlers — batch-level and
        // shipment-level projections are independent, so the batch can flip to its
        // terminal state moments before the last shipment-level update commits. Poll
        // until both the batch status and every shipment status have settled.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        CustomerBatchView? view = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var poll = await TenantGet(tenantId, $"/v1/shipments/batches/{batchId}");
            if (poll.StatusCode == HttpStatusCode.OK)
            {
                view = await poll.Content.ReadFromJsonAsync<CustomerBatchView>();
                if (view is not null
                    && view.Status == expectedBatchStatus
                    && view.Shipments.All(isShipmentSettled))
                    return view;
            }
            await Task.Delay(50);
        }
        Assert.Fail($"Read-side projection never settled (batch='{view?.Status ?? "<missing>"}', expected batch='{expectedBatchStatus}').");
        return view!;
    }

    private static Task<HttpResponseMessage> StaffGet(string url) =>
        HttpHelpers.SendAsync(HttpMethod.Get, url, body: null,
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));

    private static Task<HttpResponseMessage> StaffPost(string url, object? body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));

    private static Task<HttpResponseMessage> TenantGet(Guid tenantId, string url) =>
        HttpHelpers.SendAsync(HttpMethod.Get, url, body: null,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));

    private static Task<HttpResponseMessage> TenantPost(Guid tenantId, string url, object? body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));

    private static ShipmentRequestDto ShipmentFor(string from, string to) => new(
        From: NewAddress(from),
        To: NewAddress(to),
        Parcel: new Parcel(
            Weight: Weight.FromGrams(750),
            Dimensions: new Dimension(200, 200, 100),
            DeclaredValue: new Money(24.99m, "EUR"),
            Reference: $"ORD-{Guid.NewGuid():N}"[..10],
            Description: "Acme widget"),
        PreferredServiceCode: "STANDARD");

    private static Address NewAddress(string countryIsoAlpha2) => new(
        Name: "Acme Customer",
        Line1: "Hoofdstraat 1",
        Line2: null,
        City: "Amsterdam",
        Region: null,
        PostalCode: "1012 AB",
        Country: new CountryCode(countryIsoAlpha2));
}
