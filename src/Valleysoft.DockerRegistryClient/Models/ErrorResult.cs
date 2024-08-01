using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

// https://github.com/opencontainers/distribution-spec/blob/main/spec.md#error-codes
public class ErrorResult
{
    [JsonPropertyName("errors")]
    public Error[] Errors { get; set; } = Array.Empty<Error>();
}
