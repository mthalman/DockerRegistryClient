namespace Valleysoft.DockerRegistryClient.Credentials;

public interface IRegistryClientCredentials
{
    Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
