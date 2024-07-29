using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class ErrorResult
{
    [JsonPropertyName("errors")]
    public Error[] Errors { get; set; } = Array.Empty<Error>();
}
