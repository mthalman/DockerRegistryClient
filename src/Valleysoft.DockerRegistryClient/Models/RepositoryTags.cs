﻿using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;
 
public class RepositoryTags
{
    [JsonProperty("name")]
    public string? RepositoryName { get; set; }

    [JsonProperty("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}
