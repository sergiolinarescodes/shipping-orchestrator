using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Email;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Identity;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;
using Wolverine;

namespace ShippingOrchestrator.Application.UnitTests.Identity;

[TestFixture]
public class RequestMagicLinkHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static IClock ClockAt(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static IOptions<AuthOptions> Opts() => Options.Create(new AuthOptions
    {
        VerifyEndpointBaseUrl = "http://localhost:5101",
        DashboardBaseUrl = "http://localhost:5173",
        MagicLinkTtlSeconds = 900,
    });

    [Test]
    public async Task Unknown_email_returns_success_without_writing_token_or_publishing()
    {
        var accounts = Substitute.For<IAccountRepository>();
        accounts.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        var tenants = Substitute.For<ITenantRepository>();
        tenants.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Tenant>());
        var tokens = Substitute.For<IMagicLinkTokenRepository>();
        var invitations = Substitute.For<ITenantInvitationRepository>();
        invitations.ListPendingByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TenantInvitation>());
        var bus = Substitute.For<IMessageBus>();
        var uow = Substitute.For<IUnitOfWork>();

        var result = await RequestMagicLinkHandler.Handle(
            new RequestMagicLinkCommand("nobody@example.com", null),
            accounts, tenants, tokens, invitations, uow, bus, Opts(), ClockAt(Now), CancellationToken.None);

        result.Should().NotBeNull();
        await tokens.DidNotReceive().AddAsync(Arg.Any<MagicLinkToken>(), Arg.Any<CancellationToken>());
        await bus.DidNotReceive().PublishAsync(Arg.Any<SendEmailCommand>());
    }

    [Test]
    public async Task Known_email_writes_token_and_publishes_email_command()
    {
        var email = "owner@example.com";
        var accounts = Substitute.For<IAccountRepository>();
        accounts.FindByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(Account.Create(email, Now));
        var tenants = Substitute.For<ITenantRepository>();
        var tokens = Substitute.For<IMagicLinkTokenRepository>();
        var invitations = Substitute.For<ITenantInvitationRepository>();
        invitations.ListPendingByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TenantInvitation>());
        var bus = Substitute.For<IMessageBus>();
        var uow = Substitute.For<IUnitOfWork>();

        await RequestMagicLinkHandler.Handle(
            new RequestMagicLinkCommand(email, "ip-hash"),
            accounts, tenants, tokens, invitations, uow, bus, Opts(), ClockAt(Now), CancellationToken.None);

        await tokens.Received(1).AddAsync(
            Arg.Is<MagicLinkToken>(t => t.Email == email && t.IpHash == "ip-hash"),
            Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.Received(1).PublishAsync(Arg.Is<SendEmailCommand>(c =>
            c.Message.To == email && c.Message.HtmlBody.Contains("verify")));
    }
}
