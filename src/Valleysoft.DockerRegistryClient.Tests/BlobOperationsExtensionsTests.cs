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
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":[]}}"""));
        var operations = new Mock<IBlobOperations>();
        operations
            .Setup(value => value.GetAsync("repo", "sha256:config", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var image = await operations.Object.GetImageAsync("repo", "sha256:config");

        Assert.Equal("amd64", image.Architecture);
        Assert.Equal("linux", image.Os);
        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task GetImageAsync_InvalidJson_ThrowsContextualJsonException()
    {
        var operations = new Mock<IBlobOperations>();
        operations
            .Setup(value => value.GetAsync("repo", "sha256:layer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("not-json")));

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => operations.Object.GetImageAsync("repo", "sha256:layer"));

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
        var expected = new BlobUploadResult("/v2/repo/blobs/sha256:abc", "sha256:abc");
        using var stream = new MemoryStream([1, 2, 3]);
        var operations = new Mock<IBlobOperations>();
        operations
            .Setup(value => value.BeginUploadAsync("repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialization);
        operations
            .Setup(value => value.EndUploadAsync(
                initialization.Location,
                "sha256:abc",
                context,
                stream,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        BlobUploadResult result = await operations.Object.UploadAsync("repo", stream, "sha256:abc");

        Assert.Same(expected, result);
        operations.VerifyAll();
    }
}
