using System.Net;
using System.Net.Http.Headers;
using Moq;
using Valleysoft.DockerRegistryClient.Credentials;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RegistryOperationValidationTests
{
    private const string Repository = "team/project/image";
    private static readonly string Digest = $"sha256:{new string('a', 64)}";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../repo")]
    [InlineData("repo?x=1")]
    [InlineData("repo#fragment")]
    [InlineData("repo%2Fimage")]
    [InlineData("repo\\image")]
    [InlineData("répo")]
    public async Task InvalidRepository_AllOperations_FailBeforeAuthentication(string? repository)
    {
        var credentials = new Mock<IRegistryClientCredentials>(MockBehavior.Strict);
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new RegistryClient("registry.example", credentials.Object, httpClient);
        using var stream = new UntouchedStream();
        Func<Task>[] operations =
        [
            () => client.Blobs.GetAsync(repository!, Digest),
            () => client.Blobs.GetRangeAsync(repository!, Digest, 0),
            () => client.Blobs.ExistsAsync(repository!, Digest),
            () => client.Blobs.DeleteAsync(repository!, Digest),
            () => client.Blobs.BeginUploadAsync(repository!),
            () => client.Blobs.UploadAsync(repository!, stream, Digest),
            () => client.Manifests.GetAsync(repository!, "latest"),
            () => client.Manifests.ExistsAsync(repository!, "latest"),
            () => client.Manifests.GetDigestAsync(repository!, "latest"),
            () => client.Manifests.PublishAsync(repository!, "latest", ReadOnlyMemory<byte>.Empty, "application/example"),
            () => client.Manifests.DeleteAsync(repository!, Digest),
            () => client.Manifests.DeleteTagAsync(repository!, "latest"),
            () => client.Tags.GetAsync(repository!),
            () => client.Referrers.GetAsync(repository!, Digest)
        ];
        foreach (Func<Task> operation in operations)
        {
            ArgumentException exception = await Assert.ThrowsAnyAsync<ArgumentException>(operation);
            Assert.Equal(repository is null ? typeof(ArgumentNullException) : typeof(ArgumentException), exception.GetType());
            Assert.Equal("repositoryName", exception.ParamName);
        }
        credentials.VerifyNoOtherCalls();
        Assert.False(stream.WasAccessed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256:abc")]
    [InlineData("other:value?x=1")]
    [InlineData("other:value#fragment")]
    [InlineData("other:value&x=1")]
    [InlineData("other:value/segment")]
    [InlineData("other:value%23fragment")]
    public async Task InvalidDigest_AllOperations_FailBeforeAuthenticationOrStreamAccess(string? digest)
    {
        var credentials = new Mock<IRegistryClientCredentials>(MockBehavior.Strict);
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new RegistryClient("registry.example", credentials.Object, httpClient);
        using var stream = new UntouchedStream();
        var context = new BlobUploadContext(null);
        (Func<Task> Operation, string Parameter)[] operations =
        [
            (() => client.Blobs.GetAsync(Repository, digest!), "digest"),
            (() => client.Blobs.GetRangeAsync(Repository, digest!, 0), "digest"),
            (() => client.Blobs.ExistsAsync(Repository, digest!), "digest"),
            (() => client.Blobs.DeleteAsync(Repository, digest!), "digest"),
            (() => client.Blobs.UploadAsync(Repository, stream, digest!), "digest"),
            (() => client.Blobs.EndUploadAsync("/upload", digest!, context, stream), "digest"),
            (() => client.Manifests.GetAsync(Repository, digest!), "tagOrDigest"),
            (() => client.Manifests.ExistsAsync(Repository, digest!), "digest"),
            (() => client.Manifests.GetDigestAsync(Repository, digest!), "tagOrDigest"),
            (() => client.Manifests.PublishAsync(Repository, digest!, ReadOnlyMemory<byte>.Empty, "application/example"), "tagOrDigest"),
            (() => client.Manifests.DeleteAsync(Repository, digest!), "digest"),
            (() => client.Referrers.GetAsync(Repository, digest!), "digest")
        ];
        foreach ((Func<Task> operation, string parameter) in operations)
        {
            ArgumentException exception = await Assert.ThrowsAnyAsync<ArgumentException>(operation);
            Assert.Equal(digest is null ? typeof(ArgumentNullException) : typeof(ArgumentException), exception.GetType());
            Assert.Equal(parameter, exception.ParamName);
        }
        credentials.VerifyNoOtherCalls();
        Assert.False(stream.WasAccessed);
    }

    [Theory]
    [InlineData("sourceRepositoryName")]
    [InlineData("destinationRepositoryName")]
    [InlineData("sourceReference")]
    [InlineData("destinationReference")]
    public async Task Copy_InvalidArguments_FailBeforeEitherClientSends(string parameter)
    {
        var credentials = new Mock<IRegistryClientCredentials>(MockBehavior.Strict);
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var source = new RegistryClient("source.example", credentials.Object, httpClient);
        using var destination = new RegistryClient("destination.example", credentials.Object, httpClient);
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => source.CopyAsync(
            parameter == "sourceRepositoryName" ? "../source" : Repository,
            parameter == "sourceReference" ? "sha256:abc" : "latest",
            destination,
            parameter == "destinationRepositoryName" ? "dest#injected" : Repository,
            parameter == "destinationReference" ? "tag?injected" : "latest"));
        Assert.Equal(parameter, exception.ParamName);
        credentials.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadConvenience_InvalidDigest_DoesNotStartSession()
    {
        var operations = new Mock<IBlobOperations>(MockBehavior.Strict);
        using var stream = new UntouchedStream();
        await Assert.ThrowsAsync<ArgumentException>(() => operations.Object.UploadAsync(Repository, stream, "sha256:abc"));
        operations.VerifyNoOtherCalls();
        Assert.False(stream.WasAccessed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest#fragment")]
    [InlineData("latest?query")]
    [InlineData("other:abc")]
    public async Task DeleteTag_InvalidTag_IdentifiesPublicParameter(string? tag)
    {
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new RegistryClient("registry.example", null, httpClient);
        ArgumentException exception = await Assert.ThrowsAnyAsync<ArgumentException>(
            () => client.Manifests.DeleteTagAsync(Repository, tag!));
        Assert.Equal("tag", exception.ParamName);
    }

    [Fact]
    public async Task LazyPagination_InvalidReferences_FailOnFirstMove()
    {
        var credentials = new Mock<IRegistryClientCredentials>(MockBehavior.Strict);
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new RegistryClient("registry.example", credentials.Object, httpClient);
        await using var tags = client.Tags.GetAllAsync("repo#fragment").GetAsyncEnumerator();
        await using var referrers = client.Referrers.GetAllAsync(Repository, "sha256:abc").GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentException>(async () => await tags.MoveNextAsync());
        await Assert.ThrowsAsync<ArgumentException>(async () => await referrers.MoveNextAsync());
        credentials.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Operations_SendExpectedNestedEndpoints()
    {
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new RegistryClient("https://bücher.example:5443", null, httpClient);
        string blobPath = $"/v2/{Repository}/blobs/{Digest}";
        string manifestPath = $"/v2/{Repository}/manifests/latest";
        string uploadPath = $"/v2/{Repository}/blobs/uploads/";
        (Func<Task> Operation, HttpMethod Method, string Path, string Query)[] cases =
        [
            (async () => { using Stream result = await client.Blobs.GetAsync(Repository, Digest); }, HttpMethod.Get, blobPath, ""),
            (async () => { BlobDownloadResult result = await client.Blobs.GetRangeAsync(Repository, Digest, 0); using Stream content = result.Content; }, HttpMethod.Get, blobPath, ""),
            (() => client.Blobs.ExistsAsync(Repository, Digest), HttpMethod.Head, blobPath, ""),
            (() => client.Blobs.DeleteAsync(Repository, Digest), HttpMethod.Delete, blobPath, ""),
            (() => client.Blobs.BeginUploadAsync(Repository), HttpMethod.Post, uploadPath, ""),
            (() => client.Manifests.GetAsync(Repository, "latest"), HttpMethod.Get, manifestPath, ""),
            (() => client.Manifests.ExistsAsync(Repository, "latest"), HttpMethod.Head, manifestPath, ""),
            (() => client.Manifests.GetDigestAsync(Repository, "latest"), HttpMethod.Head, manifestPath, ""),
            (() => client.Manifests.PublishAsync(Repository, "latest", ReadOnlyMemory<byte>.Empty, "application/example"), HttpMethod.Put, manifestPath, ""),
            (() => client.Manifests.DeleteTagAsync(Repository, "latest"), HttpMethod.Delete, manifestPath, ""),
            (() => client.Tags.GetAsync(Repository, 50), HttpMethod.Get, $"/v2/{Repository}/tags/list", "?n=50"),
            (() => client.Catalog.GetAsync(50), HttpMethod.Get, "/v2/_catalog", "?n=50"),
            (() => client.Referrers.GetAsync(Repository, Digest, "a/b+c&d"), HttpMethod.Get, $"/v2/{Repository}/referrers/{Digest}", "?artifactType=a%2Fb%2Bc%26d")
        ];
        foreach ((Func<Task> operation, HttpMethod method, string path, string query) in cases)
        {
            HttpResponseMessage response = CreateResponse();
            handler.AddExpectedRequest(request =>
            {
                Assert.Equal(method, request.Method);
                Assert.Equal("xn--bcher-kva.example", request.RequestUri!.IdnHost);
                Assert.Equal(5443, request.RequestUri.Port);
                Assert.Equal(path, request.RequestUri.AbsolutePath);
                Assert.Equal(query, request.RequestUri.Query);
                Assert.Equal("", request.RequestUri.Fragment);
                return true;
            }, response);
            await operation();
            Assert.Equal(0, handler.RemainingRequestCount);
        }
    }

    [Theory]
    [InlineData("/opaque/upload?state=a%2Fb%3D#fragment")]
    [InlineData("https://registry.example/opaque/upload?state=a%2Fb%3D#fragment")]
    public async Task UploadCompletion_AppendsDigestBeforeFragment(string location)
    {
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new RegistryClient("registry.example", null, httpClient);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/opaque/upload", request.RequestUri!.AbsolutePath);
            Assert.Equal($"?state=a%2Fb%3D&digest=sha256%3A{new string('a', 64)}", request.RequestUri.Query);
            Assert.Equal("#fragment", request.RequestUri.Fragment);
            return true;
        }, CreateResponse());
        await client.Blobs.EndUploadAsync(location, Digest, new BlobUploadContext(null));
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    private static HttpResponseMessage CreateResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/example");
        response.Headers.Location = new Uri("/opaque/result?state=value", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", "custom:result");
        response.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        response.Headers.Add("Range", "0-0");
        return response;
    }

    private sealed class UntouchedStream : MemoryStream
    {
        public bool WasAccessed { get; private set; }

        public override bool CanSeek
        {
            get
            {
                WasAccessed = true;
                throw new InvalidOperationException("The stream must not be inspected for invalid arguments.");
            }
        }
    }
}
