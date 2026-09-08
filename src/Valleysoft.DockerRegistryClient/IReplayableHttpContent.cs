namespace Valleysoft.DockerRegistryClient;

internal interface IReplayableHttpContent
{
    void PrepareForReplay();
}
