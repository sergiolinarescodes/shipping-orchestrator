using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Connections;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.UnitTests.Connections;

/// <summary>
/// Tenant-isolation guard regression: the only mutation a tenant can make on a connection
/// it doesn't own is disconnect (hard-delete). This file pins the handler-level invariant
/// directly so a future refactor that drops the ownership check fails fast without needing
/// the full E2E stack. The matching E2E test
/// (<c>HappyPathWooCommerceWebhookTests.Disconnect_rejects_cross_tenant_id_with_403</c>)
/// proves the endpoint maps <see cref="UnauthorizedAccessException"/> to HTTP 403.
/// </summary>
[TestFixture]
public class CrossTenantConnectionGuardTests
{
    private static EcommerceConnection InstallActiveConnection(TenantId owner)
    {
        var connection = EcommerceConnection.Install(
            owner, "shopify", "owner-store.myshopify.com", [0x00], DateTimeOffset.UtcNow);
        connection.MarkVerified(DateTimeOffset.UtcNow);
        return connection;
    }

    [Test]
    public async Task Disconnect_throws_UnauthorizedAccess_and_does_not_remove_when_caller_does_not_own_connection()
    {
        var ownerTenant = TenantId.New();
        var attackerTenant = TenantId.New();
        var connection = InstallActiveConnection(ownerTenant);

        var repo = Substitute.For<IEcommerceConnectionRepository>();
        repo.FindAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);
        var uow = Substitute.For<IUnitOfWork>();

        var act = () => DisconnectEcommerceConnectionHandler.Handle(
            new DisconnectEcommerceConnectionCommand(connection.Id, attackerTenant, "spoof"),
            repo, uow, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repo.DidNotReceive().Remove(Arg.Any<EcommerceConnection>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Disconnect_throws_KeyNotFound_when_connection_id_is_unknown()
    {
        var repo = Substitute.For<IEcommerceConnectionRepository>();
        repo.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EcommerceConnection?)null);
        var uow = Substitute.For<IUnitOfWork>();

        var act = () => DisconnectEcommerceConnectionHandler.Handle(
            new DisconnectEcommerceConnectionCommand(Guid.NewGuid(), TenantId.New(), "spoof"),
            repo, uow, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.DidNotReceive().Remove(Arg.Any<EcommerceConnection>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Disconnect_calls_remove_and_saves_when_caller_owns_connection()
    {
        var owner = TenantId.New();
        var connection = InstallActiveConnection(owner);

        var repo = Substitute.For<IEcommerceConnectionRepository>();
        repo.FindAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);
        var uow = Substitute.For<IUnitOfWork>();

        var result = await DisconnectEcommerceConnectionHandler.Handle(
            new DisconnectEcommerceConnectionCommand(connection.Id, owner, "tenant requested"),
            repo, uow, CancellationToken.None);

        result.ConnectionId.Should().Be(connection.Id);
        repo.Received(1).Remove(connection);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
