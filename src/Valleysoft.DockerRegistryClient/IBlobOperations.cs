using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient;
 
public interface IBlobOperations
{
    Task<HttpOperationResponse<Stream>> GetWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse<bool>> ExistsWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse> DeleteWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse> GetUploadWithHttpMessagesAsync(
        string uploadLocation, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse> DeleteUploadWithHttpMessagesAsync(
        string uploadLocation, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse<BlobUploadContext>> BeginUploadWithHttpMessagesAsync(
        string repositoryName, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse> SendUploadStreamWithHttpMessagesAsync(
        string uploadLocation, Stream stream, BlobUploadContext uploadContext, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse> EndUploadWithHttpMessagesAsync(
        string uploadLocation, string digest, BlobUploadContext uploadContext, Stream? stream = null, CancellationToken cancellationToken = default);
}
