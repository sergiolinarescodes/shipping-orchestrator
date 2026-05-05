using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Identity;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.UnitTests.Identity;

[TestFixture]
public class ConsumeMagicLinkHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static IClock ClockAt(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static IOptions<AuthOptions> Opts() =>
        Options.Create(new AuthOptions { MagicLinkTtlSeconds = 900, SessionTtlSeconds = 60 * 60 * 24 * 30 });

    [Test]
    public async Task Returns_failure_when_token_unknown()
    {
        var tokens = Substitute.For<IMagicLinkTokenRepository>();
        tokens.FindByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MagicLinkToken?)null);

        var result = await ConsumeMagicLinkHandler.Handle(
            new ConsumeMagicLinkCommand("anything"),
            tokens,
            Substitute.For<IAccountRepository>(),
            Substitute.For<ITenantRepository>(),
            Substitute.For<ITenantMembershipRepository>(),
            Substitute.For<ITenantInvitationRepository>(),
            Substitute.For<IAuthSessionRepository>(),
            Substitute.For<IUnitOfWork>(),
            Opts(),
            ClockAt(Now),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("unknown-token");
    }

    [Test]
    public async Task Returns_failure_when_token_already_consumed()
    {
        var raw = TokenGenerator.NewRawSecret();
        var hash = TokenGenerator.Hash(raw);
        var token = MagicLinkToken.Issue("user@example.com", hash, Now, TimeSpan.FromMinutes(15));
        token.Consume(Now.AddMinutes(1));

        var tokens = Substitute.For<IMagicLinkTokenRepository>();
        tokens.FindByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(token);

        var result = await ConsumeMagicLinkHandler.Handle(
            new ConsumeMagicLinkCommand(raw),
            tokens,
            Substitute.For<IAccountRepository>(),
            Substitute.For<ITenantRepository>(),
            Substitute.For<ITenantMembershipRepository>(),
            Substitute.For<ITenantInvitationRepository>(),
            Substitute.For<IAuthSessionRepository>(),
            Substitute.For<IUnitOfWork>(),
            Opts(),
            ClockAt(Now.AddMinutes(2)),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("already-consumed");
    }

    [Test]
    public async Task Happy_path_creates_account_grants_membership_and_issues_session()
    {
        var raw = TokenGenerator.NewRawSecret();
        var hash = TokenGenerator.Hash(raw);
        var email = "owner@example.com";
        var token = MagicLinkToken.Issue(email, hash, Now, TimeSpan.FromMinutes(15));

        var tokenRepo = Substitute.For<IMagicLinkTokenRepository>();
        tokenRepo.FindByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(token);

        var accounts = Substitute.For<IAccountRepository>();
        accounts.FindByEmailAsync(email, Arg.Any<CancellationToken>()).Returns((Account?)null);

        var matchingTenant = Tenant.Create("Acme", email, Now);
        var tenants = Substitute.For<ITenantRepository>();
        tenants.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { matchingTenant });

        var memberships = Substitute.For<ITenantMembershipRepository>();
        memberships.FindAsync(Arg.Any<AccountId>(), Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        var invitations = Substitute.For<ITenantInvitationRepository>();
        invitations.ListPendingByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TenantInvitation>());

        var sessions = Substitute.For<IAuthSessionRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var result = await ConsumeMagicLinkHandler.Handle(
            new ConsumeMagicLinkCommand(raw),
            tokenRepo,
            accounts,
            tenants,
            memberships,
            invitations,
            sessions,
            uow,
            Opts(),
            ClockAt(Now.AddMinutes(1)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AccountId.Should().NotBeNull();
        result.RawSessionToken.Should().NotBeNullOrEmpty();
        await accounts.Received(1).AddAsync(
            Arg.Is<Account>(a => a.Email == email), Arg.Any<CancellationToken>());
        await memberships.Received(1).AddAsync(
            Arg.Is<TenantMembership>(m => m.TenantId == matchingTenant.Id && m.Role == MembershipRole.Owner),
            Arg.Any<CancellationToken>());
        await sessions.Received(1).AddAsync(Arg.Any<AuthSession>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        token.ConsumedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Pending_invitation_is_consumed_and_grants_membership()
    {
        var raw = TokenGenerator.NewRawSecret();
        var hash = TokenGenerator.Hash(raw);
        var email = "invitee@example.com";
        var inviter = AccountId.New();
        var tenantId = TenantId.New();

        var token = MagicLinkToken.Issue(email, hash, Now, TimeSpan.FromMinutes(15));
        var invitation = TenantInvitation.Create(tenantId, email, inviter, MembershipRole.Member, Now);

        var tokenRepo = Substitute.For<IMagicLinkTokenRepository>();
        tokenRepo.FindByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(token);

        var accounts = Substitute.For<IAccountRepository>();
        accounts.FindByEmailAsync(email, Arg.Any<CancellationToken>()).Returns((Account?)null);

        var tenants = Substitute.For<ITenantRepository>();
        tenants.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Tenant>());

        var memberships = Substitute.For<ITenantMembershipRepository>();
        memberships.FindAsync(Arg.Any<AccountId>(), Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        var invitations = Substitute.For<ITenantInvitationRepository>();
        invitations.ListPendingByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(new[] { invitation });

        var sessions = Substitute.For<IAuthSessionRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var result = await ConsumeMagicLinkHandler.Handle(
            new ConsumeMagicLinkCommand(raw),
            tokenRepo, accounts, tenants, memberships, invitations, sessions, uow,
            Opts(), ClockAt(Now.AddMinutes(1)), CancellationToken.None);

        result.Success.Should().BeTrue();
        invitation.ConsumedAt.Should().NotBeNull();
        await memberships.Received(1).AddAsync(
            Arg.Is<TenantMembership>(m => m.TenantId == tenantId && m.Role == MembershipRole.Member),
            Arg.Any<CancellationToken>());
    }
}
