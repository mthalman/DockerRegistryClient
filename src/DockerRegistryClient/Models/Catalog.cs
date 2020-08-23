using System.Collections.Generic;
using Newtonsoft.Json;

namespace DockerRegistry.Models
{
    public class Catalog
    {
        [JsonProperty("repositories")]
        public List<string> RepositoryNames { get; set; }
    }
}
