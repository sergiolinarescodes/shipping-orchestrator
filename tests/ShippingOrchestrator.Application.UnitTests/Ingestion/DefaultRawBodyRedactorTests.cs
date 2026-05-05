using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Application.Ingestion;

namespace ShippingOrchestrator.Application.UnitTests.Ingestion;

[TestFixture]
public class DefaultRawBodyRedactorTests
{
    private readonly DefaultRawBodyRedactor _redactor = new();

    [Test]
    public void Redact_strips_email_addresses()
    {
        var input = "{\"email\":\"alice@example.com\",\"name\":\"Alice\"}";
        var output = _redactor.Redact(input);
        output.Should().NotContain("alice@example.com");
        output.Should().Contain("[email]");
    }

    [Test]
    public void Redact_strips_phone_like_digit_runs()
    {
        var input = "{\"phone\":\"+31 6 12345678\"}";
        var output = _redactor.Redact(input);
        output.Should().NotContain("12345678");
        output.Should().Contain("[phone]");
    }

    [Test]
    public void Redact_masks_sensitive_json_keys()
    {
        var input = "{\"token\":\"super-secret-1234\",\"name\":\"Acme\"}";
        var output = _redactor.Redact(input);
        output.Should().Contain("\"token\":\"***\"");
        output.Should().NotContain("super-secret-1234");
        output.Should().Contain("Acme");
    }

    [Test]
    public void Redact_truncates_to_max_length()
    {
        var input = new string('x', DefaultRawBodyRedactor.MaxLength * 2);
        var output = _redactor.Redact(input);
        output.Length.Should().Be(DefaultRawBodyRedactor.MaxLength);
    }

    [Test]
    public void Hash_is_lowercase_hex_sha256()
    {
        var output = _redactor.Hash("hello"u8.ToArray());
        output.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Test]
    public void Hash_is_stable_for_identical_input()
    {
        var a = _redactor.Hash([1, 2, 3, 4]);
        var b = _redactor.Hash([1, 2, 3, 4]);
        a.Should().Be(b);
    }
}
