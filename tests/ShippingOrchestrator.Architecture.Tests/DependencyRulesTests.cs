using FluentAssertions;
using NetArchTest.Rules;
using NUnit.Framework;

namespace ShippingOrchestrator.Architecture.Tests;

[TestFixture]
public class DependencyRulesTests
{
    private const string ApplicationNs = "ShippingOrchestrator.Application";
    private const string InfrastructureNs = "ShippingOrchestrator.Infrastructure";
    private const string AbstractionsNs = "ShippingOrchestrator.Modules.Abstractions";
    private const string ReadAbstractionsNs = "ShippingOrchestrator.ReadModels.Abstractions";
    private const string ReadOpsNs = "ShippingOrchestrator.ReadModels.Operations";
    private const string ReadCustomerNs = "ShippingOrchestrator.ReadModels.Customer";
    private const string ReadProjectionsNs = "ShippingOrchestrator.ReadModels.Projections";

    [Test]
    public void Domain_has_no_dependencies_on_other_layers()
    {
        var result = Types.InAssembly(typeof(Domain.Tenancy.Tenant).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, AbstractionsNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must remain free of references to outer layers. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Test]
    public void Application_does_not_reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Application.Shipments.CreateShipmentBatchCommand).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must not call into Infrastructure. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Test]
    public void Read_platform_does_not_reference_Application_or_Infrastructure()
    {
        // Operations, Customer, and Projections (read side) must consume domain events only —
        // never the application or infrastructure layers. This is the wall.
        AssertNoDep(typeof(ReadModels.Operations.Persistence.OperationsReadDbContext), ApplicationNs, InfrastructureNs);
        AssertNoDep(typeof(ReadModels.Customer.Persistence.CustomerReadDbContext), ApplicationNs, InfrastructureNs);
        AssertNoDep(typeof(ReadModels.Projections.ShipmentProjectionHandler), ApplicationNs, InfrastructureNs);
    }

    [Test]
    public void Operations_and_Customer_do_not_reference_each_other()
    {
        // Read side now ships as one assembly with folder/namespace separation, so the
        // wall is enforced per-namespace rather than per-assembly. Projections is the only
        // place allowed to touch both sides — it is excluded from both filters.
        var assembly = typeof(ReadModels.Operations.Persistence.OperationsReadDbContext).Assembly;

        var opsResult = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith(ReadOpsNs)
            .ShouldNot().HaveDependencyOn(ReadCustomerNs)
            .GetResult();
        opsResult.IsSuccessful.Should().BeTrue(
            "Types under {0} must not reference {1}. Offenders: {2}",
            ReadOpsNs, ReadCustomerNs,
            string.Join(", ", opsResult.FailingTypeNames ?? Array.Empty<string>()));

        var customerResult = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith(ReadCustomerNs)
            .ShouldNot().HaveDependencyOn(ReadOpsNs)
            .GetResult();
        customerResult.IsSuccessful.Should().BeTrue(
            "Types under {0} must not reference {1}. Offenders: {2}",
            ReadCustomerNs, ReadOpsNs,
            string.Join(", ", customerResult.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Test]
    public void Connectors_only_depend_on_Domain_and_ModulesAbstractions()
    {
        AssertNoDep(typeof(CarrierConnectors.PostNL.PostNlConnectorModule), ApplicationNs, InfrastructureNs, ReadOpsNs, ReadCustomerNs, ReadProjectionsNs, ReadAbstractionsNs);
        AssertNoDep(typeof(EcommerceConnectors.Shopify.ShopifyConnectorModule), ApplicationNs, InfrastructureNs, ReadOpsNs, ReadCustomerNs, ReadProjectionsNs, ReadAbstractionsNs);
    }

    private static void AssertNoDep(Type marker, params string[] forbidden)
    {
        var result = Types.InAssembly(marker.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();
        result.IsSuccessful.Should().BeTrue(
            "{0} must not depend on [{1}]. Offenders: {2}",
            marker.Assembly.GetName().Name,
            string.Join(",", forbidden),
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
