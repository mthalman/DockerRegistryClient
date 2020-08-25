namespace DockerRegistry
{
    public static class ManifestMediaTypes
    {
        public const string ManifestSchema1 = "application/vnd.docker.distribution.manifest.v1+json";
        public const string ManifestSchema1Signed = "application/vnd.docker.distribution.manifest.v1+prettyjws";
        public const string ManifestSchema2 = "application/vnd.docker.distribution.manifest.v2+json";
        public const string ManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";

        public const string ContainerConfig = "application/vnd.docker.container.image.v1+json";
        public const string GzippedTar = "application/vnd.docker.image.rootfs.diff.tar.gzip";
    }
}
