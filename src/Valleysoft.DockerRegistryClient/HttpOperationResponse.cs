namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Owns an HTTP request and its response so they can be disposed together.
/// </summary>
/// <param name="Request">Request sent to the registry.</param>
/// <param name="Response">Response returned by the registry.</param>
public record HttpOperationResponse(HttpRequestMessage Request, HttpResponseMessage Response) : IDisposable
{
    /// <summary>
    /// Disposes both the request and response.
    /// </summary>
    public void Dispose()
    {
        this.Response.Dispose();
        this.Request.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Owns an HTTP exchange and exposes its processed response body.
/// </summary>
/// <typeparam name="T">Type of the processed response body.</typeparam>
public record HttpOperationResponse<T> : HttpOperationResponse
{
    /// <summary>
    /// Initializes an HTTP operation response.
    /// </summary>
    /// <param name="request">Request sent to the registry.</param>
    /// <param name="response">Response returned by the registry.</param>
    /// <param name="body">Processed response body.</param>
    public HttpOperationResponse(HttpRequestMessage request, HttpResponseMessage response, T body)
        : base(request, response)
    {
        Body = body;
    }

    /// <summary>
    /// Gets the processed response body.
    /// </summary>
    public T Body { get; }
}
