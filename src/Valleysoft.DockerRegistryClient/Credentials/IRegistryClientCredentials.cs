namespace Valleysoft.DockerRegistryClient.Credentials;

/// <summary>
/// Supplies credentials to registry HTTP requests.
/// </summary>
public interface IRegistryClientCredentials
{
    /// <summary>
    /// Applies credentials to an outgoing HTTP request.
    /// </summary>
    /// <param name="request">Request to authenticate.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
