using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Domain.UnitTests.ValueObjects;

[TestFixture]
public class CountryCodeTests
{
    [Test]
    public void Parse_lowercases_to_upper()
    {
        CountryCode.Parse("nl").Value.Should().Be("NL");
    }

    [Test]
    public void Wildcard_is_recognized()
    {
        CountryCode.Parse("*").IsWildcard.Should().BeTrue();
    }

    [TestCase("USA")]
    [TestCase("X1")]
    [TestCase("")]
    [TestCase(" ")]
    public void Invalid_codes_throw(string value)
    {
        var act = () => CountryCode.Parse(value);
        act.Should().Throw<ArgumentException>();
    }
}
