using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Valleysoft.DockerRegistryClient;

internal static class TemporaryFile
{
    private const int BufferSize = 81920;

    public static FileStream Create(string path)
    {
#if NET8_0_OR_GREATER
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
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CreateAnonymousUnixFile(path);
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
#endif
    }

#if NETSTANDARD2_0
    private static FileStream CreateAnonymousUnixFile(string path)
    {
        int createExclusiveFlags =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0xA00 : 0xC0;
        int descriptor = Open(path, openReadWrite: 2 | createExclusiveFlags, mode: 0x180);
        if (descriptor == -1)
        {
            throw new IOException(
                $"Unable to create temporary file '{path}'.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        try
        {
            if (Unlink(path) != 0)
            {
                throw new IOException(
                    $"Unable to unlink temporary file '{path}'.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return new FileStream(handle, FileAccess.ReadWrite, BufferSize);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int openReadWrite, uint mode);

    [DllImport("libc", EntryPoint = "unlink", SetLastError = true)]
    private static extern int Unlink(string path);
#endif
}
