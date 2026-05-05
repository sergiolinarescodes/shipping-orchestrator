using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using ShippingOrchestrator.Application.Shipments;
using ShippingOrchestrator.CarrierConnectors.PostNL;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.PrivateApi.Endpoints;
using ShippingOrchestrator.PublicApi.Endpoints;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;

namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// Drives the carrier-failure branch of the saga: pins
/// <see cref="PostNlInMemoryOptions.FailureProbability"/> to 1.0 for the duration of the
/// test so every label call returns an error. Asserts the batch lands in the Failed
/// terminal state and the ops view surfaces the failures.
/// </summary>
[TestFixture]
public class FailurePathTests : E2ETestBase
{
    private double _originalFailureProbability;

    [SetUp]
    public void ForceFailures()
    {
        var options = E2EFixture.Current.App.Services
            .GetRequiredService<IOptions<PostNlInMemoryOptions>>().Value;
        _originalFailureProbability = options.FailureProbability;
        options.FailureProbability = 1.0;
    }

    [TearDown]
    public void RestoreFailureProbability()
    {
        var options = E2EFixture.Current.App.Services
            .GetRequiredService<IOptions<PostNlInMemoryOptions>>().Value;
        options.FailureProbability = _originalFailureProbability;
    }

    [Test]
    public async Task Batch_lands_in_Failed_state_when_carrier_always_errors()
    {
        var tenantResponse = await StaffPost("/admin/tenants/",
            new CreateTenantHttpRequest("Acme NL Failures", "ops@acme.test"));
        tenantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenantId = (await tenantResponse.Content.ReadFromJsonAsync<CreateTenantResponse>())!.TenantId;

        await StaffPost($"/admin/tenants/{tenantId}/activate", body: null);
        await StaffPost($"/admin/tenants/{tenantId}/carrier-assignments",
            new CreateCarrierAssignmentHttpRequest(
                CarrierCode: "postnl",
                Priority: 100,
                OriginCountries: new[] { "NL", "BE" },
                DestinationCountries: new[] { "*" }));

        var batchRequest = new CreateBatchHttpRequest(new[]
        {
            ShipmentFor("NL", "BE"),
            ShipmentFor("NL", "DE"),
        });
        var batchResponse = await TenantPost(tenantId, "/v1/shipments/batches", batchRequest);
        batchResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var batchAccepted = await batchResponse.Content.ReadFromJsonAsync<CreateShipmentBatchResponse>();
        var batchId = batchAccepted!.BatchId;

        var completion = await E2EFixture.Current.BatchSignal
            .WaitAsync(batchId, TimeSpan.FromSeconds(30));
        completion.SuccessCount.Should().Be(0);
        completion.FailureCount.Should().Be(2);
        completion.FinalStatus.ToString().Should().Be("Failed");

        var view = await ReadModelBatchAsync(tenantId, batchId,
            expectedBatchStatus: "Failed",
            expectedShipmentStatus: "Failed");
        view.FailureCount.Should().Be(2);
        view.SuccessCount.Should().Be(0);

        var opsResponse = await StaffGet("/ops/queues?status=Failed");
        opsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var opsRows = await opsResponse.Content.ReadFromJsonAsync<List<OpsBatchRow>>();
        opsRows!.Should().Contain(r => r.BatchId == batchId);
    }

    private static async Task<CustomerBatchView> ReadModelBatchAsync(
        Guid tenantId, Guid batchId, string expectedBatchStatus, string expectedShipmentStatus)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        CustomerBatchView? view = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var poll = await TenantGet(tenantId, $"/v1/shipments/batches/{batchId}");
            if (poll.StatusCode == HttpStatusCode.OK)
            {
                view = await poll.Content.ReadFromJsonAsync<CustomerBatchView>();
                if (view is not null
                    && view.Status == expectedBatchStatus
                    && view.Shipments.All(s => s.Status == expectedShipmentStatus))
                    return view;
            }
            await Task.Delay(50);
        }
        Assert.Fail($"Read-side never settled (batch='{view?.Status ?? "<missing>"}', expected batch='{expectedBatchStatus}', shipments expected='{expectedShipmentStatus}').");
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
            Weight: Weight.FromGrams(500),
            Dimensions: new Dimension(150, 150, 80),
            DeclaredValue: new Money(10m, "EUR")),
        PreferredServiceCode: "STANDARD");

    private static Address NewAddress(string country) => new(
        Name: "Acme",
        Line1: "Hoofdstraat 1",
        Line2: null,
        City: "Amsterdam",
        Region: null,
        PostalCode: "1012 AB",
        Country: new CountryCode(country));
}
