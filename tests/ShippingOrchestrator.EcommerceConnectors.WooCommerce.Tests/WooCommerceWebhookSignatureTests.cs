using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce.Tests;

[TestFixture]
public class WooCommerceWebhookSignatureTests
{
    [Test]
    public void Validates_matching_hmac_signature()
    {
        var body = "{\"id\":1,\"currency\":\"EUR\"}"u8.ToArray();
        const string secret = "test-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToBase64String(hmac.ComputeHash(body));

        WooCommerceEcommerceConnector.TryValidateWebhookSignature(body, sig, secret).Should().BeTrue();
    }

    [Test]
    public void Rejects_tampered_body()
    {
        var body = "{\"id\":1}"u8.ToArray();
        const string secret = "test-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToBase64String(hmac.ComputeHash(body));
        var tamperedBody = "{\"id\":2}"u8.ToArray();

        WooCommerceEcommerceConnector.TryValidateWebhookSignature(tamperedBody, sig, secret).Should().BeFalse();
    }

    [Test]
    public void Rejects_wrong_secret()
    {
        var body = "{\"id\":1}"u8.ToArray();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("real-secret"));
        var sig = Convert.ToBase64String(hmac.ComputeHash(body));

        WooCommerceEcommerceConnector.TryValidateWebhookSignature(body, sig, "wrong-secret").Should().BeFalse();
    }

    [Test]
    public void Rejects_empty_inputs()
    {
        WooCommerceEcommerceConnector.TryValidateWebhookSignature(Array.Empty<byte>(), string.Empty, "secret").Should().BeFalse();
        WooCommerceEcommerceConnector.TryValidateWebhookSignature(Array.Empty<byte>(), "abc", string.Empty).Should().BeFalse();
    }
}
