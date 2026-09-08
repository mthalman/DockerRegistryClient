namespace Valleysoft.DockerRegistryClient;
 
/// <summary>
/// Defines media types used by supported Docker and OCI manifests.
/// </summary>
public static class ManifestMediaTypes
{
    /// <summary>
    /// Docker Image Manifest Version 2, Schema 2 media type.
    /// </summary>
    public const string DockerManifestSchema2 = "application/vnd.docker.distribution.manifest.v2+json";

    /// <summary>
    /// Docker manifest list media type.
    /// </summary>
    public const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";

    /// <summary>
    /// OCI image manifest version 1 media type.
    /// </summary>
    public const string OciManifestSchema1 = "application/vnd.oci.image.manifest.v1+json";

    /// <summary>
    /// OCI image index version 1 media type.
    /// </summary>
    public const string OciImageIndex1 = "application/vnd.oci.image.index.v1+json";

    //public const string DockerContainerConfig = "application/vnd.docker.container.image.v1+json";
    //public const string DockerGzippedTar = "application/vnd.docker.image.rootfs.diff.tar.gzip";
}
