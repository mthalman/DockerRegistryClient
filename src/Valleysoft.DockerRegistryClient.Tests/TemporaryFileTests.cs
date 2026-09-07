using System.Runtime.InteropServices;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class TemporaryFileTests
{
    [Fact]
    public void Create_OnUnix_GrantsAccessOnlyToOwner()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string path = Path.Combine(
            Path.GetTempPath(),
            $"docker-registry-client-test-{Guid.NewGuid():N}.tmp");
        using FileStream stream = TemporaryFile.Create(path);

        UnixFileMode mode = File.GetUnixFileMode(path);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            mode & (UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute));
    }
}
