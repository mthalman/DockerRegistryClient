using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Valleysoft.DockerRegistryClient.Credentials;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[CollectionDefinition(Name)]
public sealed class RegistryCollection : ICollectionFixture<RegistryFixture>
{
    public const string Name = "Live registry";
}

public sealed class RegistryFixture : IAsyncLifetime
{
    private const ushort RegistryPort = 5000;
    private const string UserName = "registry-user";
    private const string Password = "registry-password";
    private const string PasswordFileContent =
        "registry-user:$2b$10$KS/XLUsjdRh4XD3Nn6QR..MHbPm3xEj.1a9AMQaLhdySsekkOMR/S";

    private IContainer? container;
    private string? fixtureDirectory;

    public Uri BaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        fixtureDirectory = Path.Combine(Path.GetTempPath(), $"docker-registry-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);

        string passwordFile = Path.Combine(fixtureDirectory, "htpasswd");
        await File.WriteAllTextAsync(passwordFile, PasswordFileContent + Environment.NewLine);

        container = new ContainerBuilder("registry:3.1.1")
            .WithPortBinding(RegistryPort, true)
            .WithEnvironment("REGISTRY_STORAGE_DELETE_ENABLED", "true")
            .WithEnvironment("REGISTRY_AUTH", "htpasswd")
            .WithEnvironment("REGISTRY_AUTH_HTPASSWD_REALM", "Registry Tests")
            .WithEnvironment("REGISTRY_AUTH_HTPASSWD_PATH", "/auth/htpasswd/htpasswd")
            .WithResourceMapping(passwordFile, "/auth/htpasswd")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("listening on"))
            .Build();

        await container.StartAsync();
        BaseUri = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(RegistryPort)}/");

        using HttpClient client = CreateHttpClient(authenticated: true);
        using HttpResponseMessage response = await client.GetAsync("v2/");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }

        if (fixtureDirectory is not null && Directory.Exists(fixtureDirectory))
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    public RegistryClient CreateClient(bool authenticated = true) =>
        authenticated
            ? new RegistryClient(BaseUri.AbsoluteUri, new BasicAuthenticationCredentials(UserName, Password))
            : new RegistryClient(BaseUri.AbsoluteUri);

    public string GetRepositoryName(string testName) =>
        $"integration/{testName.ToLowerInvariant().Replace('_', '-')}-{Guid.NewGuid():N}";

    public async Task<BlobSeed> UploadBlobAsync(string repository, byte[] content)
    {
        string digest = GetDigest(content);
        using RegistryClient client = CreateClient();
        using MemoryStream stream = new(content);
        await client.Blobs.UploadAsync(repository, stream, digest);
        return new BlobSeed(digest, content.LongLength);
    }

    public async Task<ManifestSeed> PutManifestAsync(
        string repository,
        string reference,
        string mediaType,
        object manifest)
    {
        string json = JsonSerializer.Serialize(manifest);
        using HttpClient client = CreateHttpClient(authenticated: true);
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            $"v2/{repository}/manifests/{reference}")
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };

        using HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string digest = response.Headers.GetValues("Docker-Content-Digest").Single();
        return new ManifestSeed(digest, Encoding.UTF8.GetByteCount(json), json);
    }

    public static string GetDigest(byte[] content) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";

    private HttpClient CreateHttpClient(bool authenticated)
    {
        HttpClient client = new() { BaseAddress = BaseUri };
        if (authenticated)
        {
            string value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserName}:{Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", value);
        }

        return client;
    }
}

public sealed record BlobSeed(string Digest, long Size);

public sealed record ManifestSeed(string Digest, long Size, string Json);
