using System.Collections.Generic;
using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models
{
    public class Catalog
    {
        [JsonProperty("repositories")]
        public List<string> RepositoryNames { get; set; } = new List<string>();
    }
}
