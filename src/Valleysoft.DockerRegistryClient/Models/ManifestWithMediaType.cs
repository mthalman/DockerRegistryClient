﻿using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public abstract class ManifestWithMediaType : Manifest
{
    /// <summary>
    /// The MIME type of the manifest.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }
}
