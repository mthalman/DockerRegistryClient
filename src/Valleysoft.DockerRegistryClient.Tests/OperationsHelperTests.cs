using System.Net;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class OperationsHelperTests
{
    [Fact]
    public async Task HandleNotFoundErrorAsync_Success_ReturnsValue()
    {
        var expectedValue = "test result";
        
        var result = await OperationsHelper.HandleNotFoundErrorAsync("Not found", () => Task.FromResult(expectedValue));
        
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task HandleNotFoundErrorAsync_NotFoundError_WrapsException()
    {
        var originalException = new RegistryException("Original message")
        {
            StatusCode = HttpStatusCode.NotFound
        };
        
        var ex = await Assert.ThrowsAsync<RegistryException>(() => 
            OperationsHelper.HandleNotFoundErrorAsync<string>("Blob not found.", () => Task.FromException<string>(originalException)));
        
        Assert.Equal("Blob not found.", ex.Message);
        Assert.Same(originalException, ex.InnerException);
    }

    [Fact]
    public async Task HandleNotFoundErrorAsync_OtherError_ThrowsOriginal()
    {
        var originalException = new RegistryException("Unauthorized")
        {
            StatusCode = HttpStatusCode.Unauthorized
        };
        
        var ex = await Assert.ThrowsAsync<RegistryException>(() => 
            OperationsHelper.HandleNotFoundErrorAsync<string>("Blob not found.", () => Task.FromException<string>(originalException)));
        
        Assert.Equal("Unauthorized", ex.Message);
        Assert.Null(ex.InnerException);
    }
}
