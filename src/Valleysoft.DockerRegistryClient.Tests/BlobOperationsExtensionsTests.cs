using System.Text;
using System.Text.Json;
using Moq;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class BlobOperationsExtensionsTests
{
    [Fact]
    public async Task GetImageAsync_ValidImageConfig_DeserializesAndDisposesStream()
    {
        byte[] content = Encoding.UTF8.GetBytes(
            """{"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":[]}}""");
        string digest = RegistryFixture.GetDigest(content);
        var stream = new MemoryStream(content);
        var operations = new Mock<IBlobOperations>();
        operations
            .Setup(value => value.GetAsync("repo", digest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var image = await operations.Object.GetImageAsync("repo", digest);

        Assert.Equal("amd64", image.Architecture);
        Assert.Equal("linux", image.Os);
        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task GetImageAsync_InvalidJson_ThrowsContextualJsonException()
    {
        byte[] content = Encoding.UTF8.GetBytes("not-json");
        string digest = RegistryFixture.GetDigest(content);
        var operations = new Mock<IBlobOperations>();
        operations
            .Setup(value => value.GetAsync("repo", digest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(content));

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => operations.Object.GetImageAsync("repo", digest));

        Assert.Contains("Verify the digest represents an image config", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task UploadAsync_ForwardsInitializationContextToEndUpload()
    {
        var context = new BlobUploadContext(null);
        var initialization = new BlobUploadInitializationResult(
            "/v2/repo/blobs/uploads/id",
            Guid.NewGuid(),
            context);
        string digest = RegistryFixture.GetDigest([1, 2, 3]);
        var expected = new BlobUploadResult($"/v2/repo/blobs/{digest}", digest);
        using var stream = new MemoryStream([1, 2, 3]);
        var operations = new Mock<IBlobOperations>();
        operations
            .Setup(value => value.BeginUploadAsync("repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialization);
        operations
            .Setup(value => value.EndUploadAsync(
                initialization.Location,
                digest,
                context,
                stream,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        BlobUploadResult result = await operations.Object.UploadAsync("repo", stream, digest);

        Assert.Same(expected, result);
        operations.VerifyAll();
    }
}
