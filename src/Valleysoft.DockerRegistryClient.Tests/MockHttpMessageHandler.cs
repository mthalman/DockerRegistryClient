using System.Net;

namespace Valleysoft.DockerRegistryClient.Tests;

/// <summary>
/// Mock HTTP message handler for testing HTTP requests without a real server.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(Func<HttpRequestMessage, bool> matcher, HttpResponseMessage response)> _expectedRequests = new();

    public void AddExpectedRequest(Func<HttpRequestMessage, bool> matcher, HttpResponseMessage response)
    {
        _expectedRequests.Enqueue((matcher, response));
    }

    public void AddExpectedRequest(string expectedUri, HttpResponseMessage response)
    {
        AddExpectedRequest(req => req.RequestUri?.ToString() == expectedUri, response);
    }

    public void AddExpectedRequest(HttpMethod method, string expectedUri, HttpResponseMessage response)
    {
        AddExpectedRequest(req => req.Method == method && req.RequestUri?.ToString() == expectedUri, response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_expectedRequests.TryDequeue(out var expected))
        {
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        }

        if (!expected.matcher(request))
        {
            throw new InvalidOperationException($"Request did not match expected pattern: {request.Method} {request.RequestUri}");
        }

        return Task.FromResult(expected.response);
    }

    public int RemainingRequestCount => _expectedRequests.Count;
}
