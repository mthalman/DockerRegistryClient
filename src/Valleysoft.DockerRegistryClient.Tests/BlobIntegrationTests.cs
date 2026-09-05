using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[Collection(RegistryCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BlobIntegrationTests
{
    private readonly RegistryFixture fixture;

    public BlobIntegrationTests(RegistryFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task BlobOperations_ExerciseUploadReadExistenceAndDelete()
    {
        string repository = fixture.GetRepositoryName(nameof(BlobOperations_ExerciseUploadReadExistenceAndDelete));
        byte[] content = Encoding.UTF8.GetBytes("live registry blob");
        string digest = RegistryFixture.GetDigest(content);
        using RegistryClient client = fixture.CreateClient();
        using MemoryStream uploadStream = new(content);

        BlobUploadResult upload = await client.Blobs.UploadAsync(repository, uploadStream, digest);

        Assert.Equal(digest, upload.Digest);
        Assert.Contains($"/v2/{repository}/blobs/{digest}", upload.Location);
        Assert.True(await client.Blobs.ExistsAsync(repository, digest));
        Assert.False(await client.Blobs.ExistsAsync(repository, RegistryFixture.GetDigest([0x01])));

        await using (Stream download = await client.Blobs.GetAsync(repository, digest))
        {
            using MemoryStream downloadedContent = new();
            await download.CopyToAsync(downloadedContent);
            Assert.Equal(content, downloadedContent.ToArray());
        }

        BlobDownloadResult boundedDownload = await client.Blobs.GetRangeAsync(repository, digest, 5, 8);
        await using (boundedDownload.Content)
        {
            Assert.True(boundedDownload.IsRangeHonored);
            Assert.Equal(5, boundedDownload.RangeStart);
            Assert.Equal(12, boundedDownload.RangeEnd);
            Assert.Equal(content.Length, boundedDownload.TotalLength);
            Assert.Equal(content.Skip(5).Take(8), await ReadAllBytesAsync(boundedDownload.Content));
        }

        BlobDownloadResult resumedDownload = await client.Blobs.GetRangeAsync(repository, digest, 5);
        await using (resumedDownload.Content)
        {
            Assert.True(resumedDownload.IsRangeHonored);
            Assert.Equal(content.Skip(5), await ReadAllBytesAsync(resumedDownload.Content));
        }

        await client.Blobs.DeleteAsync(repository, digest);
        Assert.False(await client.Blobs.ExistsAsync(repository, digest));

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Blobs.GetAsync(repository, digest));
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains(exception.Errors, error => error.Code == "BLOB_UNKNOWN");
    }

    [Fact]
    public async Task BlobOperations_ExerciseChunkedUploadStatusCompletionAndCancellation()
    {
        string repository = fixture.GetRepositoryName(nameof(BlobOperations_ExerciseChunkedUploadStatusCompletionAndCancellation));
        byte[] firstChunk = Encoding.UTF8.GetBytes("first-");
        byte[] finalChunk = Encoding.UTF8.GetBytes("second");
        byte[] content = [.. firstChunk, .. finalChunk];
        string digest = RegistryFixture.GetDigest(content);
        using RegistryClient client = fixture.CreateClient();

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync(repository);
        Assert.NotEqual(Guid.Empty, initialization.UploadId);

        BlobUpload initialStatus = await client.Blobs.GetUploadAsync(initialization.Location);
        Assert.Equal(initialization.UploadId, initialStatus.UploadId);
        Assert.Equal(0, initialStatus.RangeOffset);

        using MemoryStream firstStream = new(firstChunk);
        BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
            initialization.Location,
            firstStream,
            initialization.UploadContext);

        Assert.Equal(initialization.UploadId, chunk.UploadId);
        Assert.Equal(firstChunk.Length - 1, chunk.RangeOffset);

        BlobUpload chunkStatus = await client.Blobs.GetUploadAsync(chunk.Location);
        Assert.Equal(chunk.UploadId, chunkStatus.UploadId);
        Assert.Equal(chunk.RangeOffset, chunkStatus.RangeOffset);

        using MemoryStream finalStream = new(finalChunk);
        BlobUploadResult completed = await client.Blobs.EndUploadAsync(
            chunk.Location,
            digest,
            initialization.UploadContext,
            finalStream);
        Assert.Equal(digest, completed.Digest);
        Assert.True(await client.Blobs.ExistsAsync(repository, digest));

        BlobUploadInitializationResult canceled = await client.Blobs.BeginUploadAsync(repository);
        await client.Blobs.DeleteUploadAsync(canceled.Location);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Blobs.GetUploadAsync(canceled.Location));
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains(exception.Errors, error => error.Code == "BLOB_UPLOAD_UNKNOWN");
    }

    [Fact]
    public async Task GetImageAsync_ReturnsImageConfigModel()
    {
        string repository = fixture.GetRepositoryName(nameof(GetImageAsync_ReturnsImageConfigModel));
        byte[] content = Encoding.UTF8.GetBytes(
            """{"created":"2026-01-02T03:04:05Z","author":"integration-tests","architecture":"amd64","os":"linux","config":{"Env":["MODE=test"],"Cmd":["run"]},"rootfs":{"type":"layers","diff_ids":[]},"history":[]}""");
        BlobSeed blob = await fixture.UploadBlobAsync(repository, content);
        using RegistryClient client = fixture.CreateClient();

        var image = await client.Blobs.GetImageAsync(repository, blob.Digest);

        Assert.Equal("integration-tests", image.Author);
        Assert.Equal("amd64", image.Architecture);
        Assert.Equal("linux", image.Os);
        Assert.Equal(["MODE=test"], image.Config!.EnvironmentVariables);
        Assert.Equal(["run"], image.Config.CommandArgs);
    }

    [Fact]
    public async Task RegistryAuthentication_RejectsAnonymousAndAcceptsBasicCredentials()
    {
        using RegistryClient anonymousClient = fixture.CreateClient(authenticated: false);

        AuthenticationException exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => anonymousClient.Catalog.GetAsync());

        Assert.Contains("unauthorized response", exception.Message);

        using RegistryClient authenticatedClient = fixture.CreateClient();
        Page<Models.Catalog> catalog = await authenticatedClient.Catalog.GetAsync();
        Assert.NotNull(catalog.Value.RepositoryNames);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using MemoryStream destination = new();
        await stream.CopyToAsync(destination);
        return destination.ToArray();
    }
}
