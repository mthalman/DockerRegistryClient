namespace Valleysoft.DockerRegistryClient.Models;

public class Page<T>
{
    public Page(T value, string? nextPageLink)
    {
        Value = value;
        NextPageLink = nextPageLink;
    }

    public string? NextPageLink { get; }

    public T Value { get; }
}
