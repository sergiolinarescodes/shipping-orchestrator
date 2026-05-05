using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.PerformanceTests.Driver;

internal static class PerfPayloadFactory
{
    public static EcommerceOrderPayload Sample(TenantId tenant, string externalOrderId)
    {
        var origin = new Address("Origin", "Line 1", null, "Hoofddorp", null, "2132 AA", new CountryCode("NL"));
        var dest = new Address("Customer", "Line 1", null, "Amsterdam", null, "1012 LM", new CountryCode("NL"));
        return new EcommerceOrderPayload(
            TenantId: tenant,
            ConnectorCode: "shopify",
            ExternalOrderId: externalOrderId,
            Currency: "EUR",
            From: origin,
            To: dest,
            Items: [new EcommerceOrderLineItem("SKU", "Widget", 1, new Money(20m, "EUR"), Weight.FromGrams(500))],
            TotalWeight: Weight.FromGrams(500),
            PackageDimensions: new Dimension(100, 100, 50));
    }
}
