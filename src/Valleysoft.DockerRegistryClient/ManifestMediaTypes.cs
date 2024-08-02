namespace Valleysoft.DockerRegistryClient;
 
public static class ManifestMediaTypes
{
    public const string DockerManifestSchema2 = "application/vnd.docker.distribution.manifest.v2+json";
    public const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";

    public const string OciManifestSchema1 = "application/vnd.oci.image.manifest.v1+json";
    public const string OciImageIndex1 = "application/vnd.oci.image.index.v1+json";

    //public const string DockerContainerConfig = "application/vnd.docker.container.image.v1+json";
    //public const string DockerGzippedTar = "application/vnd.docker.image.rootfs.diff.tar.gzip";
}
