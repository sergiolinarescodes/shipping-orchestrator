using System.Net.Http.Json;

namespace ShippingOrchestrator.E2E.Tests.Infrastructure;

internal static class HttpHelpers
{
    public static Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body,
        params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(method, url);
        foreach (var (name, value) in headers)
            request.Headers.Add(name, value);
        if (body is not null) request.Content = JsonContent.Create(body);
        return E2EFixture.Current.HttpClient.SendAsync(request);
    }
}
