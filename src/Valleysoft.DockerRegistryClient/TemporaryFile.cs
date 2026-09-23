namespace Valleysoft.DockerRegistryClient;

internal static class TemporaryFile
{
    private const int BufferSize = 81920;

    public static FileStream Create(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    BufferSize = BufferSize,
                    Options = FileOptions.Asynchronous |
                        FileOptions.DeleteOnClose |
                        FileOptions.SequentialScan,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                });
        }

        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous |
                FileOptions.DeleteOnClose |
                FileOptions.SequentialScan);
    }
}
