namespace Valleysoft.DockerRegistryClient;

internal sealed class BlobMountResult
{
    private BlobMountResult(bool isMounted, BlobUploadSession? upload)
    {
        IsMounted = isMounted;
        Upload = upload;
    }

    public BlobMountResult(BlobUploadSession upload)
        : this(isMounted: false, upload)
    {
    }

    public static BlobMountResult Mounted { get; } =
        new(isMounted: true, upload: null);

    public static BlobMountResult Unsupported { get; } =
        new(isMounted: false, upload: null);

    public bool IsMounted { get; }

    public BlobUploadSession? Upload { get; }
}

internal sealed class BlobUploadSession
{
    public BlobUploadSession(string location, BlobUploadContext uploadContext)
    {
        Location = location;
        UploadContext = uploadContext;
    }

    public string Location { get; }

    public BlobUploadContext UploadContext { get; }
}
