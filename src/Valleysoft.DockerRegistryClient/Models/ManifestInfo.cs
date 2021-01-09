using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models
{
    public class ManifestInfo
    {
        public ManifestInfo(string mediaType, string dockerContentDigest, Manifest manifest)
        {
            this.MediaType = mediaType;
            this.DockerContentDigest = dockerContentDigest;
            this.Manifest = manifest;
        }

        public string MediaType { get; }
        public string DockerContentDigest { get; }
        public Manifest Manifest { get; }
    }
}
