namespace Valleysoft.DockerRegistryClient;


public record HttpOperationResponse(HttpRequestMessage Request, HttpResponseMessage Response) : IDisposable
{
    public void Dispose()
    {
        this.Response?.Dispose();
        this.Request?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record HttpOperationResponse<T> : HttpOperationResponse
{
    public HttpOperationResponse(HttpRequestMessage request, HttpResponseMessage response, T body)
        : base(request, response)
    {
        Body = body;
    }

    public T Body { get; }
}
