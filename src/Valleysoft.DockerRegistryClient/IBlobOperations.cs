namespace Valleysoft.DockerRegistryClient;

public interface IBlobOperations
{
    Task<Stream> GetAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task<BlobUpload> GetUploadAsync(
        string uploadLocation, CancellationToken cancellationToken = default);

    Task DeleteUploadAsync(
        string uploadLocation, CancellationToken cancellationToken = default);

    Task<BlobUploadInitializationResult> BeginUploadAsync(
        string repositoryName, CancellationToken cancellationToken = default);

    Task<BlobUploadStreamResult> SendUploadStreamAsync(
        string uploadLocation, Stream stream, BlobUploadContext uploadContext, CancellationToken cancellationToken = default);

    Task<BlobUploadResult> EndUploadAsync(
        string uploadLocation, string digest, BlobUploadContext uploadContext, Stream? stream = null, CancellationToken cancellationToken = default);
}
