using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.UnitTests.Identity;

[TestFixture]
public class AuthSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly AccountId Acct = AccountId.New();

    [Test]
    public void Issue_creates_active_session_without_tenant()
    {
        var session = AuthSession.Issue(Acct, "hash", Now, TimeSpan.FromDays(30));
        session.AccountId.Should().Be(Acct);
        session.CurrentTenantId.Should().BeNull();
        session.IsActive(Now).Should().BeTrue();
        session.ExpiresAt.Should().Be(Now.AddDays(30));
    }

    [Test]
    public void Touch_extends_expiry_only_forward()
    {
        var session = AuthSession.Issue(Acct, "h", Now, TimeSpan.FromDays(30));
        var initialExpiry = session.ExpiresAt;
        session.Touch(Now.AddDays(1), TimeSpan.FromDays(30));
        session.ExpiresAt.Should().BeAfter(initialExpiry);

        var bumped = session.ExpiresAt;
        // A touch with a smaller sliding window must NEVER shorten expiry.
        session.Touch(Now.AddDays(2), TimeSpan.FromMinutes(1));
        session.ExpiresAt.Should().Be(bumped);
    }

    [Test]
    public void SelectTenant_sets_current_and_can_be_changed()
    {
        var session = AuthSession.Issue(Acct, "h", Now, TimeSpan.FromDays(30));
        var t1 = TenantId.New();
        var t2 = TenantId.New();
        session.SelectTenant(t1, Now);
        session.CurrentTenantId.Should().Be(t1);
        session.SelectTenant(t2, Now.AddMinutes(1));
        session.CurrentTenantId.Should().Be(t2);
    }

    [Test]
    public void Revoke_makes_session_inactive()
    {
        var session = AuthSession.Issue(Acct, "h", Now, TimeSpan.FromDays(30));
        session.Revoke(Now);
        session.IsActive(Now).Should().BeFalse();
    }
}
