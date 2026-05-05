using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Domain.UnitTests.Identity;

[TestFixture]
public class MagicLinkTokenTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public void Issue_sets_expiry_and_normalizes_email()
    {
        var token = MagicLinkToken.Issue("Foo@Bar.com", "hash123", Now, TimeSpan.FromMinutes(15));
        token.Email.Should().Be("foo@bar.com");
        token.ExpiresAt.Should().Be(Now.AddMinutes(15));
        token.IsConsumable(Now).Should().BeTrue();
    }

    [Test]
    public void Consume_marks_consumed_and_blocks_reuse()
    {
        var token = MagicLinkToken.Issue("a@b.com", "h", Now, TimeSpan.FromMinutes(15));
        token.Consume(Now.AddMinutes(1));
        token.ConsumedAt.Should().Be(Now.AddMinutes(1));

        Action again = () => token.Consume(Now.AddMinutes(2));
        again.Should().Throw<InvalidOperationException>();
        token.IsConsumable(Now.AddMinutes(2)).Should().BeFalse();
    }

    [Test]
    public void Consume_after_expiry_throws()
    {
        var token = MagicLinkToken.Issue("a@b.com", "h", Now, TimeSpan.FromMinutes(15));
        Action act = () => token.Consume(Now.AddMinutes(16));
        act.Should().Throw<InvalidOperationException>();
        token.ConsumedAt.Should().BeNull();
    }
}
