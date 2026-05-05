using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Domain.UnitTests.Identity;

[TestFixture]
public class AccountTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public void Create_normalizes_email_to_lowercase_trimmed()
    {
        var account = Account.Create("  Hello@Example.COM  ", Now);
        account.Email.Should().Be("hello@example.com");
        account.CreatedAt.Should().Be(Now);
        account.LastSignInAt.Should().BeNull();
        account.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AccountCreated>()
            .Which.Email.Should().Be("hello@example.com");
    }

    [Test]
    public void RecordSignIn_updates_LastSignInAt_and_raises_event()
    {
        var account = Account.Create("a@b.com", Now);
        account.ClearDomainEvents();
        var later = Now.AddMinutes(5);
        account.RecordSignIn(later);
        account.LastSignInAt.Should().Be(later);
        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountSignedIn>();
    }

    [Test]
    public void Create_rejects_empty_email()
    {
        Action act = () => Account.Create("   ", Now);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void NormalizeEmail_is_idempotent()
    {
        Account.NormalizeEmail("Foo@Bar.com").Should().Be("foo@bar.com");
        Account.NormalizeEmail("foo@bar.com").Should().Be("foo@bar.com");
    }
}
