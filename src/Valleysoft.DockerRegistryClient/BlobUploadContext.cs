using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// State associated with the initialization of the blob upload.
/// </summary>
public class BlobUploadContext
{
    internal BlobUploadContext(AuthenticationHeaderValue? authorization)
    {
        Authorization = authorization;
    }

    internal AuthenticationHeaderValue? Authorization { get; }
}
